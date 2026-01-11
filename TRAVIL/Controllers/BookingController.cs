using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly ILogger<BookingController> _logger;

        public BookingController(
            IBookingService bookingService,
            ILogger<BookingController> logger)
        {
            _bookingService = bookingService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new booking
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (request.NumberOfRooms <= 0)
                return BadRequest(new { success = false, message = "Number of rooms must be at least 1" });

            if (request.NumberOfGuests <= 0)
                return BadRequest(new { success = false, message = "Number of guests must be at least 1" });

            var result = await _bookingService.CreateBookingAsync(
                userId,
                request.PackageId,
                request.NumberOfRooms,
                request.NumberOfGuests);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            _logger.LogInformation($"Booking created: {result.Booking.BookingReference}");

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = new
                {
                    bookingId = result.Booking.BookingId,
                    bookingReference = result.Booking.BookingReference,
                    totalPrice = result.Booking.TotalPrice,
                    status = result.Booking.Status.ToString()
                }
            });
        }


        /// <summary>
        /// Get user's bookings
        /// </summary>
        [HttpGet("my-bookings")]
        [Authorize]
        public async Task<IActionResult> GetMyBookings()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var bookings = await _bookingService.GetUserBookingsAsync(userId);

            var result = bookings.Select(b => new
            {
                bookingId = b.BookingId,
                bookingReference = b.BookingReference,
                packageId = b.PackageId,  // <-- ADD THIS LINE (was missing)
                destination = b.TravelPackage?.Destination,
                country = b.TravelPackage?.Country,
                startDate = b.TravelPackage?.StartDate,
                endDate = b.TravelPackage?.EndDate,
                numberOfRooms = b.NumberOfRooms,
                numberOfGuests = b.NumberOfGuests,
                totalPrice = b.TotalPrice,
                status = b.Status.ToString(),
                bookingDate = b.BookingDate,
                confirmedDate = b.ConfirmedDate,
                paymentStatus = b.Payment?.Status.ToString() ?? "Pending",
                daysUntilTrip = b.TravelPackage != null ?
                    (int)(b.TravelPackage.StartDate - DateTime.UtcNow).TotalDays : 0,
                imageUrl = b.TravelPackage?.ImageUrl ?? b.TravelPackage?.Images?.FirstOrDefault()?.ImageUrl,
                // Optional: include more package details
                travelPackage = b.TravelPackage != null ? new
                {
                    packageId = b.TravelPackage.PackageId,
                    destination = b.TravelPackage.Destination,
                    country = b.TravelPackage.Country,
                    description = b.TravelPackage.Description,
                    imageUrl = b.TravelPackage.ImageUrl
                } : null
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }







        /// <summary>
        /// Get booking by ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetBooking(int id)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var booking = await _bookingService.GetBookingByIdAsync(id);
            if (booking == null)
                return NotFound(new { success = false, message = "Booking not found" });

            // Check if user owns this booking or is admin
            var isAdmin = User.IsInRole("Admin");
            if (booking.UserId != userId && !isAdmin)
                return Forbid();

            return Ok(new
            {
                success = true,
                data = new
                {
                    bookingId = booking.BookingId,
                    bookingReference = booking.BookingReference,
                    user = new
                    {
                        userId = booking.User?.UserId,
                        name = $"{booking.User?.FirstName} {booking.User?.LastName}",
                        email = booking.User?.Email
                    },
                    package = new
                    {
                        packageId = booking.TravelPackage?.PackageId,
                        destination = booking.TravelPackage?.Destination,
                        country = booking.TravelPackage?.Country,
                        startDate = booking.TravelPackage?.StartDate,
                        endDate = booking.TravelPackage?.EndDate,
                        description = booking.TravelPackage?.Description,
                        itinerary = booking.TravelPackage?.Itinerary,
                        imageUrl = booking.TravelPackage?.ImageUrl
                    },
                    numberOfRooms = booking.NumberOfRooms,
                    numberOfGuests = booking.NumberOfGuests,
                    totalPrice = booking.TotalPrice,
                    status = booking.Status.ToString(),
                    bookingDate = booking.BookingDate,
                    confirmedDate = booking.ConfirmedDate,
                    cancelledDate = booking.CancelledDate,
                    cancellationReason = booking.CancellationReason,
                    payment = booking.Payment != null ? new
                    {
                        paymentId = booking.Payment.PaymentId,
                        amount = booking.Payment.Amount,
                        status = booking.Payment.Status.ToString(),
                        paymentDate = booking.Payment.PaymentDate,
                        transactionId = booking.Payment.TransactionId
                    } : null
                }
            });
        }

        /// <summary>
        /// Cancel a booking
        /// </summary>
        [HttpPost("{id}/cancel")]
        [Authorize]
        public async Task<IActionResult> CancelBooking(int id, [FromBody] CancelBookingRequest request)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var booking = await _bookingService.GetBookingByIdAsync(id);
            if (booking == null)
                return NotFound(new { success = false, message = "Booking not found" });

            // Check if user owns this booking or is admin
            var isAdmin = User.IsInRole("Admin");
            if (booking.UserId != userId && !isAdmin)
                return Forbid();

            var result = await _bookingService.CancelBookingAsync(id, request?.Reason ?? "User requested cancellation");

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            _logger.LogInformation($"Booking cancelled: {booking.BookingReference}");

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Check if user can book
        /// </summary>
        [HttpGet("can-book")]
        [Authorize]
        public async Task<IActionResult> CanBook()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var canBook = await _bookingService.CanUserBookAsync(userId);
            var activeCount = await _bookingService.GetActiveBookingCountAsync(userId);

            return Ok(new
            {
                success = true,
                canBook = canBook,
                activeBookings = activeCount,
                maxBookings = 3,
                remainingSlots = 3 - activeCount
            });
        }

        // ===== WAITING LIST ENDPOINTS =====

        /// <summary>
        /// Join waiting list
        /// </summary>
        [HttpPost("waiting-list")]
        [Authorize]
        public async Task<IActionResult> JoinWaitingList([FromBody] JoinWaitingListRequest request)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _bookingService.JoinWaitingListAsync(userId, request.PackageId, request.NumberOfRooms);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new
            {
                success = true,
                message = result.Message,
                position = result.Entry?.Position
            });
        }

        /// <summary>
        /// Leave waiting list
        /// </summary>
        [HttpDelete("waiting-list/{packageId}")]
        [Authorize]
        public async Task<IActionResult> LeaveWaitingList(int packageId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _bookingService.LeaveWaitingListAsync(userId, packageId);

            if (!result)
                return NotFound(new { success = false, message = "You are not in the waiting list for this package" });

            return Ok(new { success = true, message = "You have been removed from the waiting list" });
        }

        /// <summary>
        /// Get user's waiting list entries
        /// </summary>
        [HttpGet("waiting-list/my")]
        [Authorize]
        public async Task<IActionResult> GetMyWaitingList()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var entries = await _bookingService.GetUserWaitingListAsync(userId);

            var result = entries.Select(e => new
            {
                packageId = e.PackageId,
                destination = e.TravelPackage?.Destination,
                country = e.TravelPackage?.Country,
                startDate = e.TravelPackage?.StartDate,
                numberOfRooms = e.NumberOfRooms,
                position = e.Position,
                dateAdded = e.DateAdded,
                isNotified = e.IsNotified,
                dateNotified = e.DateNotified
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }

        // ===== ADMIN ENDPOINTS =====

        /// <summary>
        /// Get all bookings (Admin only)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllBookings()
        {
            var bookings = await _bookingService.GetAllBookingsAsync();

            var result = bookings.Select(b => new
            {
                bookingId = b.BookingId,
                bookingReference = b.BookingReference,
                userName = $"{b.User?.FirstName} {b.User?.LastName}",
                userEmail = b.User?.Email,
                destination = b.TravelPackage?.Destination,
                startDate = b.TravelPackage?.StartDate,
                numberOfRooms = b.NumberOfRooms,
                totalPrice = b.TotalPrice,
                status = b.Status.ToString(),
                paymentStatus = b.Payment?.Status.ToString() ?? "Pending",
                bookingDate = b.BookingDate
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get waiting list for a package (Admin only)
        /// </summary>
        [HttpGet("waiting-list/package/{packageId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPackageWaitingList(int packageId)
        {
            var entries = await _bookingService.GetWaitingListAsync(packageId);

            var result = entries.Select(e => new
            {
                waitingListId = e.WaitingListId,
                userId = e.UserId,
                userName = $"{e.User?.FirstName} {e.User?.LastName}",
                userEmail = e.User?.Email,
                numberOfRooms = e.NumberOfRooms,
                position = e.Position,
                dateAdded = e.DateAdded,
                isNotified = e.IsNotified,
                dateNotified = e.DateNotified
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get bookings by package (Admin only)
        /// </summary>
        [HttpGet("package/{packageId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPackageBookings(int packageId)
        {
            var bookings = await _bookingService.GetBookingsByPackageAsync(packageId);

            var result = bookings.Select(b => new
            {
                bookingId = b.BookingId,
                bookingReference = b.BookingReference,
                userName = $"{b.User?.FirstName} {b.User?.LastName}",
                userEmail = b.User?.Email,
                numberOfRooms = b.NumberOfRooms,
                numberOfGuests = b.NumberOfGuests,
                totalPrice = b.TotalPrice,
                status = b.Status.ToString(),
                paymentStatus = b.Payment?.Status.ToString() ?? "Pending",
                bookingDate = b.BookingDate
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Confirm a booking (Admin only)
        /// </summary>
        [HttpPost("{id}/confirm")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ConfirmBooking(int id)
        {
            var result = await _bookingService.ConfirmBookingAsync(id);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }
    }

    public class CreateBookingRequest
    {
        public int PackageId { get; set; }
        public int NumberOfRooms { get; set; }
        public int NumberOfGuests { get; set; }
    }

    public class CancelBookingRequest
    {
        public string Reason { get; set; }
    }

    public class JoinWaitingListRequest
    {
        public int PackageId { get; set; }
        public int NumberOfRooms { get; set; } = 1;
    }
}
