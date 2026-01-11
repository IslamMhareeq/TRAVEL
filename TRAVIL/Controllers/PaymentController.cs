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
        private readonly IPaymentService _paymentService;
        private readonly IBookingService _bookingService;
        private readonly TravelDbContext _context;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            IBookingService bookingService,
            TravelDbContext context,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _bookingService = bookingService;
            _context = context;
            _logger = logger;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        /// <summary>
        /// Process payment for a booking (Credit Card)
        /// IMPORTANT: Card details are NOT stored
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequestDto request)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "User not authenticated" });

            // Validate request
            if (request.BookingId <= 0)
                return BadRequest(new { success = false, message = "Invalid booking ID" });

            if (string.IsNullOrEmpty(request.CardNumber))
                return BadRequest(new { success = false, message = "Card number is required" });

            if (string.IsNullOrEmpty(request.CardHolderName))
                return BadRequest(new { success = false, message = "Card holder name is required" });

            if (string.IsNullOrEmpty(request.CVV))
                return BadRequest(new { success = false, message = "CVV is required" });

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
        /// Initiate PayPal payment
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
                // Generate PayPal order ID
                var orderId = "PAYPAL-" + GenerateTransactionId();
                var baseUrl = $"{Request.Scheme}://{Request.Host}";

                var returnUrl = request.ReturnUrl ?? $"{baseUrl}/booking/payment/success?bookingId={request.BookingId}&orderId={orderId}";

                _logger.LogInformation($"PayPal payment initiated for booking {request.BookingId}, Order: {orderId}");

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        orderId = orderId,
                        approvalUrl = returnUrl,
                        status = "CREATED"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"PayPal initiation failed for booking {request.BookingId}");
                return StatusCode(500, new { success = false, message = "Failed to initiate PayPal payment" });
            }
        }

        /// <summary>
        /// Capture/Complete PayPal payment - handles directly without card validation
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
                // Get booking directly from database
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

                // Create PayPal payment record directly (bypass card validation)
                var payment = new Payment
                {
                    BookingId = request.BookingId,
                    Amount = booking.TotalPrice,
                    PaymentMethod = PaymentMethod.PayPal,
                    Status = PaymentStatus.Completed,
                    TransactionId = request.OrderId,
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

                _logger.LogInformation($"PayPal payment captured for booking {request.BookingId}, Order: {request.OrderId}");

                return Ok(new
                {
                    success = true,
                    message = "Payment successful",
                    data = new
                    {
                        paymentId = payment.PaymentId,
                        transactionId = request.OrderId,
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

        // Helper method
        private string GenerateTransactionId()
        {
            return DateTime.UtcNow.ToString("yyyyMMddHHmmss") + new Random().Next(1000, 9999);
        }
    }

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