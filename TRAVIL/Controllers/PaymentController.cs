using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRAVEL.Data;
using TRAVEL.Models;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class PaymentController : ControllerBase
    {
        private readonly TravelDbContext _context;
        private readonly IPaymentService _paymentService;
        private readonly IBookingService _bookingService;
        private readonly IPayPalService _payPalService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            TravelDbContext context,
            IPaymentService paymentService,
            IBookingService bookingService,
            IPayPalService payPalService,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _paymentService = paymentService;
            _bookingService = bookingService;
            _payPalService = payPalService;
            _logger = logger;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        /// <summary>
        /// Process credit card payment
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequestDto request)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (request.BookingId <= 0)
                return BadRequest(new { success = false, message = "Invalid booking ID" });

            // Verify booking belongs to user
            var booking = await _bookingService.GetBookingByIdAsync(request.BookingId);
            if (booking == null)
                return NotFound(new { success = false, message = "Booking not found" });

            if (booking.UserId != userId)
                return Forbid();

            // Process payment
            var paymentRequest = new PaymentRequest
            {
                CardNumber = request.CardNumber,
                CardHolderName = request.CardHolderName,
                ExpiryMonth = request.ExpiryMonth,
                ExpiryYear = request.ExpiryYear,
                CVV = request.CVV,
                PaymentMethod = request.PaymentMethod
            };

            var result = await _paymentService.ProcessPaymentAsync(request.BookingId, paymentRequest);

            if (!result.Success)
            {
                _logger.LogWarning($"Payment failed for booking {request.BookingId}: {result.Message}");
                return BadRequest(new { success = false, message = result.Message });
            }

            _logger.LogInformation($"Payment successful for booking {request.BookingId}");

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = new
                {
                    paymentId = result.Payment.PaymentId,
                    transactionId = result.Payment.TransactionId,
                    amount = result.Payment.Amount,
                    status = result.Payment.Status.ToString(),
                    paymentDate = result.Payment.PaymentDate
                }
            });
        }

        /// <summary>
        /// Initiate PayPal payment - Returns approval URL for redirection
        /// </summary>
        [HttpPost("paypal/initiate")]
        [Authorize]
        public async Task<IActionResult> InitiatePayPalPayment([FromBody] PayPalInitiateDto request)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (request.BookingId <= 0)
                return BadRequest(new { success = false, message = "Invalid booking ID" });

            // Verify booking belongs to user
            var booking = await _bookingService.GetBookingByIdAsync(request.BookingId);
            if (booking == null)
                return NotFound(new { success = false, message = "Booking not found" });

            if (booking.UserId != userId)
                return Forbid();

            // Check if already paid
            var existingPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.BookingId == request.BookingId && p.Status == PaymentStatus.Completed);

            if (existingPayment != null)
                return BadRequest(new { success = false, message = "Booking already paid" });

            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var returnUrl = request.ReturnUrl ?? $"{baseUrl}/booking/payment/success?bookingId={request.BookingId}";
                var cancelUrl = request.CancelUrl ?? $"{baseUrl}/booking/payment/{request.BookingId}?cancelled=true";

                // Create PayPal order using PayPal Service
                var paypalResponse = await _payPalService.CreateOrderAsync(
                    booking.TotalPrice,
                    "USD",
                    booking.BookingReference,
                    returnUrl,
                    cancelUrl
                );

                if (!paypalResponse.Success)
                {
                    _logger.LogError($"PayPal order creation failed: {paypalResponse.ErrorMessage}");
                    return StatusCode(500, new { success = false, message = paypalResponse.ErrorMessage ?? "Failed to create PayPal order" });
                }

                // Store PayPal order ID temporarily (you might want to save this to database)
                // For now, we'll return it to the client

                _logger.LogInformation($"PayPal payment initiated for booking {request.BookingId}, Order: {paypalResponse.OrderId}");

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        orderId = paypalResponse.OrderId,
                        approvalUrl = paypalResponse.ApprovalUrl,  // URL to redirect user to PayPal
                        status = paypalResponse.Status,
                        isSimulated = paypalResponse.IsSimulated
                    },
                    message = paypalResponse.IsSimulated
                        ? "PayPal sandbox mode - Click approval URL to simulate payment"
                        : "Redirect user to approvalUrl to complete payment"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"PayPal initiation failed for booking {request.BookingId}");
                return StatusCode(500, new { success = false, message = "Failed to initiate PayPal payment" });
            }
        }

        /// <summary>
        /// Capture/Complete PayPal payment after user approval
        /// Called after user returns from PayPal
        /// </summary>
        [HttpPost("paypal/capture")]
        [Authorize]
        public async Task<IActionResult> CapturePayPalPayment([FromBody] PayPalCaptureDto request)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (request.BookingId <= 0)
                return BadRequest(new { success = false, message = "Invalid booking ID" });

            if (string.IsNullOrEmpty(request.OrderId))
                return BadRequest(new { success = false, message = "PayPal order ID is required" });

            try
            {
                // Get booking
                var booking = await _context.Bookings
                    .Include(b => b.TravelPackage)
                    .FirstOrDefaultAsync(b => b.BookingId == request.BookingId);

                if (booking == null)
                    return NotFound(new { success = false, message = "Booking not found" });

                if (booking.UserId != userId)
                    return Forbid();

                // Check if already paid
                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.BookingId == request.BookingId && p.Status == PaymentStatus.Completed);

                if (existingPayment != null)
                    return BadRequest(new { success = false, message = "Booking already paid" });

                // Capture payment via PayPal
                var captureResponse = await _payPalService.CaptureOrderAsync(request.OrderId);

                if (!captureResponse.Success)
                {
                    _logger.LogError($"PayPal capture failed: {captureResponse.ErrorMessage}");
                    return BadRequest(new { success = false, message = captureResponse.ErrorMessage ?? "PayPal capture failed" });
                }

                // Create payment record
                var payment = new Payment
                {
                    BookingId = request.BookingId,
                    Amount = booking.TotalPrice,
                    PaymentMethod = PaymentMethod.PayPal,
                    Status = PaymentStatus.Completed,
                    TransactionId = captureResponse.TransactionId ?? request.OrderId,
                    PaymentDate = DateTime.UtcNow,
                    CompletedDate = DateTime.UtcNow
                };

                _context.Payments.Add(payment);

                // Update booking status
                booking.Status = BookingStatus.Confirmed;

                // Update available rooms
                if (booking.TravelPackage != null)
                {
                    booking.TravelPackage.AvailableRooms -= booking.NumberOfRooms;
                    if (booking.TravelPackage.AvailableRooms < 0)
                        booking.TravelPackage.AvailableRooms = 0;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"PayPal payment captured for booking {request.BookingId}, Transaction: {captureResponse.TransactionId}");

                return Ok(new
                {
                    success = true,
                    message = "Payment successful",
                    data = new
                    {
                        paymentId = payment.PaymentId,
                        transactionId = captureResponse.TransactionId,
                        orderId = request.OrderId,
                        amount = payment.Amount,
                        status = "Completed",
                        paymentDate = payment.PaymentDate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"PayPal capture failed for booking {request.BookingId}");
                return StatusCode(500, new { success = false, message = "Failed to capture PayPal payment", error = ex.Message });
            }
        }

        /// <summary>
        /// Handle PayPal return - Called when user returns from PayPal
        /// This is a GET endpoint for the redirect
        /// </summary>
        [HttpGet("paypal/return")]
        public async Task<IActionResult> PayPalReturn([FromQuery] string token, [FromQuery] string PayerID, [FromQuery] int bookingId)
        {
            _logger.LogInformation($"PayPal return: token={token}, PayerID={PayerID}, bookingId={bookingId}");

            // Redirect to payment success page with the order details
            // The frontend will then call the capture endpoint
            var redirectUrl = $"/booking/payment/success?bookingId={bookingId}&orderId={token}&payerId={PayerID}";

            return Redirect(redirectUrl);
        }

        /// <summary>
        /// Handle PayPal cancel - Called when user cancels on PayPal
        /// </summary>
        [HttpGet("paypal/cancel")]
        public IActionResult PayPalCancel([FromQuery] int bookingId)
        {
            _logger.LogInformation($"PayPal payment cancelled for booking {bookingId}");

            return Redirect($"/booking/payment/{bookingId}?cancelled=true");
        }

        /// <summary>
        /// Get payment status for a booking
        /// </summary>
        [HttpGet("booking/{bookingId}")]
        [Authorize]
        public async Task<IActionResult> GetPaymentByBooking(int bookingId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null)
                return NotFound(new { success = false, message = "Booking not found" });

            var isAdmin = User.IsInRole("Admin");
            if (booking.UserId != userId && !isAdmin)
                return Forbid();

            var payment = await _paymentService.GetPaymentByBookingAsync(bookingId);
            if (payment == null)
                return NotFound(new { success = false, message = "No payment found for this booking" });

            return Ok(new
            {
                success = true,
                data = new
                {
                    paymentId = payment.PaymentId,
                    bookingId = payment.BookingId,
                    amount = payment.Amount,
                    status = payment.Status.ToString(),
                    paymentMethod = payment.PaymentMethod.ToString(),
                    transactionId = payment.TransactionId,
                    paymentDate = payment.PaymentDate,
                    completedDate = payment.CompletedDate
                }
            });
        }

        /// <summary>
        /// Refund a payment (Admin only)
        /// </summary>
        [HttpPost("{paymentId}/refund")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RefundPayment(int paymentId, [FromBody] RefundRequest request)
        {
            var result = await _paymentService.RefundPaymentAsync(paymentId, request.Reason ?? "Admin initiated refund");

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            _logger.LogInformation($"Payment {paymentId} refunded");

            return Ok(new { success = true, message = result.Message });
        }
    }

    // DTOs
    public class PaymentRequestDto
    {
        public int BookingId { get; set; }
        public string CardNumber { get; set; } = string.Empty;
        public string CardHolderName { get; set; } = string.Empty;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string CVV { get; set; } = string.Empty;
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CreditCard;
    }

    public class PayPalInitiateDto
    {
        public int BookingId { get; set; }
        public string? ReturnUrl { get; set; }
        public string? CancelUrl { get; set; }
    }

    public class PayPalCaptureDto
    {
        public int BookingId { get; set; }
        public string OrderId { get; set; } = string.Empty;
    }

    public class RefundRequest
    {
        public string? Reason { get; set; }
    }
}