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
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ITravelPackageService _packageService;
        private readonly IBookingService _bookingService;
        private readonly IUserManagementService _userService;
        private readonly IReviewService _reviewService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ITravelPackageService packageService,
            IBookingService bookingService,
            IUserManagementService userService,
            IReviewService reviewService,
            ILogger<AdminController> logger)
        {
            _packageService = packageService;
            _bookingService = bookingService;
            _userService = userService;
            _reviewService = reviewService;
            _logger = logger;
        }

        /// <summary>
        /// Get dashboard statistics
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var packageStats = await _packageService.GetDashboardStatsAsync();
            var userStats = await _userService.GetUserStatsAsync();
            var reviewStats = await _reviewService.GetReviewStatsAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    packages = new
                    {
                        total = packageStats.TotalPackages,
                        active = packageStats.ActivePackages,
                        fullyBooked = packageStats.FullyBookedPackages,
                        onSale = packageStats.PackagesOnSale
                    },
                    bookings = new
                    {
                        total = packageStats.TotalBookings,
                        confirmed = packageStats.ConfirmedBookings,
                        pending = packageStats.PendingBookings,
                        totalRevenue = packageStats.TotalRevenue
                    },
                    users = new
                    {
                        total = userStats.TotalUsers,
                        active = userStats.ActiveUsers,
                        suspended = userStats.SuspendedUsers,
                        newThisMonth = userStats.NewUsersThisMonth
                    },
                    reviews = new
                    {
                        total = reviewStats.TotalReviews,
                        pending = reviewStats.PendingReviews,
                        averageRating = System.Math.Round(reviewStats.AverageRating, 1)
                    }
                }
            });
        }
        /// <summary>
        /// Update user information
        /// </summary>
        [HttpPut("users/{userId}")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });

                // Update user properties
                if (!string.IsNullOrEmpty(request.FirstName))
                    user.FirstName = request.FirstName;

                if (!string.IsNullOrEmpty(request.LastName))
                    user.LastName = request.LastName;

                if (!string.IsNullOrEmpty(request.Email))
                    user.Email = request.Email;

                if (!string.IsNullOrEmpty(request.PhoneNumber))
                    user.PhoneNumber = request.PhoneNumber;

                // Update role if provided
                if (!string.IsNullOrEmpty(request.Role))
                {
                    if (Enum.TryParse<TRAVEL.Models.UserRole>(request.Role, true, out var role))
                    {
                        user.Role = role;
                    }
                }

                // Update status if provided
                if (!string.IsNullOrEmpty(request.Status))
                {
                    if (Enum.TryParse<TRAVEL.Models.UserStatus>(request.Status, true, out var status))
                    {
                        user.Status = status;
                    }
                }

                user.UpdatedAt = DateTime.UtcNow;

                var result = await _userService.UpdateUserAsync(user);

                if (result)
                {
                    _logger.LogInformation($"User {userId} updated by admin");
                    return Ok(new { success = true, message = "User updated successfully" });
                }

                return BadRequest(new { success = false, message = "Failed to update user" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user {userId}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }


        // ===== USER MANAGEMENT =====

        /// <summary>
        /// Get all users
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();

            var result = users.Select(u => new
            {
                userId = u.UserId,
                firstName = u.FirstName,
                lastName = u.LastName,
                email = u.Email,
                role = u.Role.ToString(),
                status = u.Status.ToString(),
                phoneNumber = u.PhoneNumber,
                createdAt = u.CreatedAt,
                lastLoginAt = u.LastLoginAt,
                bookingsCount = u.Bookings?.Count ?? 0,
                reviewsCount = u.Reviews?.Count ?? 0
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Search users
        /// </summary>
        [HttpGet("users/search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string term)
        {
            var users = await _userService.SearchUsersAsync(term);

            var result = users.Select(u => new
            {
                userId = u.UserId,
                firstName = u.FirstName,
                lastName = u.LastName,
                email = u.Email,
                role = u.Role.ToString(),
                status = u.Status.ToString(),
                createdAt = u.CreatedAt
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get user details
        /// </summary>
        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUser(int userId)
        {
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound(new { success = false, message = "User not found" });

            return Ok(new
            {
                success = true,
                data = new
                {
                    userId = user.UserId,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    role = user.Role.ToString(),
                    status = user.Status.ToString(),
                    phoneNumber = user.PhoneNumber,
                    address = user.Address,
                    city = user.City,
                    country = user.Country,
                    postalCode = user.PostalCode,
                    createdAt = user.CreatedAt,
                    lastLoginAt = user.LastLoginAt,
                    emailVerified = user.EmailVerified,
                    bookings = user.Bookings?.Select(b => new
                    {
                        bookingId = b.BookingId,
                        bookingReference = b.BookingReference,
                        destination = b.TravelPackage?.Destination,
                        status = b.Status.ToString(),
                        totalPrice = b.TotalPrice,
                        bookingDate = b.BookingDate
                    }).ToList(),
                    waitingList = user.WaitingListEntries?.Select(w => new
                    {
                        packageId = w.PackageId,
                        destination = w.TravelPackage?.Destination,
                        position = w.Position,
                        dateAdded = w.DateAdded
                    }).ToList()
                }
            });
        }

        /// <summary>
        /// Get user booking history
        /// </summary>
        [HttpGet("users/{userId}/history")]
        public async Task<IActionResult> GetUserHistory(int userId)
        {
            var history = await _userService.GetUserBookingHistoryAsync(userId);
            if (history == null)
                return NotFound(new { success = false, message = "User not found" });

            return Ok(new { success = true, data = history });
        }

        /// <summary>
        /// Suspend user
        /// </summary>
        [HttpPost("users/{userId}/suspend")]
        public async Task<IActionResult> SuspendUser(int userId, [FromBody] SuspendUserRequest request)
        {
            var result = await _userService.SuspendUserAsync(userId, request?.Reason ?? "Admin suspended");

            if (!result)
                return BadRequest(new { success = false, message = "Cannot suspend user. User may be an admin." });

            _logger.LogInformation($"User {userId} suspended by admin");

            return Ok(new { success = true, message = "User suspended successfully" });
        }

        /// <summary>
        /// Activate user
        /// </summary>
        [HttpPost("users/{userId}/activate")]
        public async Task<IActionResult> ActivateUser(int userId)
        {
            var result = await _userService.ActivateUserAsync(userId);

            if (!result)
                return NotFound(new { success = false, message = "User not found" });

            _logger.LogInformation($"User {userId} activated by admin");

            return Ok(new { success = true, message = "User activated successfully" });
        }

        /// <summary>
        /// Delete user
        /// </summary>
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var result = await _userService.DeleteUserAsync(userId);

            if (!result)
                return BadRequest(new { success = false, message = "Cannot delete user. User may be an admin or have active bookings." });

            _logger.LogInformation($"User {userId} deleted by admin");

            return Ok(new { success = true, message = "User deleted successfully" });
        }

        /// <summary>
        /// Get user statistics
        /// </summary>
        [HttpGet("users/stats")]
        public async Task<IActionResult> GetUserStats()
        {
            var stats = await _userService.GetUserStatsAsync();
            return Ok(new { success = true, data = stats });
        }

        // ===== BOOKING RULES =====

        /// <summary>
        /// Send trip reminders (can be called by a scheduled job)
        /// </summary>
        [HttpPost("send-reminders")]
        public async Task<IActionResult> SendTripReminders()
        {
            await _bookingService.SendTripRemindersAsync();

            _logger.LogInformation("Trip reminders sent");

            return Ok(new { success = true, message = "Trip reminders sent successfully" });
        }
    }

    public class SuspendUserRequest
    {
        public string Reason { get; set; }
    }
    public class UpdateUserRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
    }
}
