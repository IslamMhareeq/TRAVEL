using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Data;
using TRAVEL.Models;
using TRAVEL.Services;
using Microsoft.EntityFrameworkCore;

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
        private readonly TravelDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ITravelPackageService packageService,
            IBookingService bookingService,
            IUserManagementService userService,
            IReviewService reviewService,
            TravelDbContext context,
            ILogger<AdminController> logger)
        {
            _packageService = packageService;
            _bookingService = bookingService;
            _userService = userService;
            _reviewService = reviewService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get dashboard statistics
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
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
                            averageRating = Math.Round(reviewStats.AverageRating, 1)
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
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
            try
            {
                var users = await _userService.GetAllUsersAsync();

                var result = users.Select(u => new
                {
                    userId = u.UserId,
                    firstName = u.FirstName,
                    lastName = u.LastName,
                    email = u.Email,
                    role = (int)u.Role,           // Return as integer: 0 = Admin, 1 = User
                    status = (int)u.Status,       // Return as integer: 0 = Active, 1 = Inactive, 2 = Suspended
                    phoneNumber = u.PhoneNumber,
                    createdAt = u.CreatedAt,
                    lastLoginAt = u.LastLoginAt,
                    bookingsCount = u.Bookings?.Count ?? 0,
                    reviewsCount = u.Reviews?.Count ?? 0
                }).ToList();

                return Ok(new { success = true, data = result, count = result.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Search users
        /// </summary>
        [HttpGet("users/search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string term)
        {
            try
            {
                var users = await _userService.SearchUsersAsync(term);

                var result = users.Select(u => new
                {
                    userId = u.UserId,
                    firstName = u.FirstName,
                    lastName = u.LastName,
                    email = u.Email,
                    role = (int)u.Role,
                    status = (int)u.Status,
                    createdAt = u.CreatedAt
                }).ToList();

                return Ok(new { success = true, data = result, count = result.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get user details
        /// </summary>
        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUser(int userId)
        {
            try
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
                        role = (int)user.Role,
                        status = (int)user.Status,
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
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user {userId}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update user information
        /// </summary>
        [HttpPut("users/{userId}")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateUserRequest request)
        {
            try
            {
                _logger.LogInformation($"UpdateUser called for userId: {userId}");
                _logger.LogInformation($"Request data: FirstName={request.FirstName}, LastName={request.LastName}, Email={request.Email}, Role={request.Role}, Status={request.Status}");

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"User {userId} not found");
                    return NotFound(new { success = false, message = "User not found" });
                }

                // Update user properties
                if (!string.IsNullOrEmpty(request.FirstName))
                    user.FirstName = request.FirstName;

                if (!string.IsNullOrEmpty(request.LastName))
                    user.LastName = request.LastName;

                if (!string.IsNullOrEmpty(request.Email))
                    user.Email = request.Email;

                if (request.PhoneNumber != null)
                    user.PhoneNumber = request.PhoneNumber;

                // Update role (0 = Admin, 1 = User)
                if (request.Role.HasValue)
                {
                    user.Role = (UserRole)request.Role.Value;
                    _logger.LogInformation($"Setting role to: {request.Role.Value}");
                }

                // Update status (0 = Active, 1 = Inactive, 2 = Suspended, 3 = Deleted)
                if (request.Status.HasValue)
                {
                    user.Status = (UserStatus)request.Status.Value;
                    _logger.LogInformation($"Setting status to: {request.Status.Value}");
                }

                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} updated successfully by admin");

                return Ok(new { success = true, message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user {userId}");
                return StatusCode(500, new { success = false, message = "Internal server error: " + ex.Message });
            }
        }

        /// <summary>
        /// Get user booking history
        /// </summary>
        [HttpGet("users/{userId}/history")]
        public async Task<IActionResult> GetUserHistory(int userId)
        {
            try
            {
                var history = await _userService.GetUserBookingHistoryAsync(userId);
                if (history == null)
                    return NotFound(new { success = false, message = "User not found" });

                return Ok(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user history for {userId}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Suspend user
        /// </summary>
        [HttpPost("users/{userId}/suspend")]
        public async Task<IActionResult> SuspendUser(int userId, [FromBody] SuspendUserRequest? request)
        {
            try
            {
                var result = await _userService.SuspendUserAsync(userId, request?.Reason ?? "Admin suspended");

                if (!result)
                    return BadRequest(new { success = false, message = "Cannot suspend user. User may be an admin." });

                _logger.LogInformation($"User {userId} suspended by admin");

                return Ok(new { success = true, message = "User suspended successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error suspending user {userId}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Activate user
        /// </summary>
        [HttpPost("users/{userId}/activate")]
        public async Task<IActionResult> ActivateUser(int userId)
        {
            try
            {
                var result = await _userService.ActivateUserAsync(userId);

                if (!result)
                    return NotFound(new { success = false, message = "User not found" });

                _logger.LogInformation($"User {userId} activated by admin");

                return Ok(new { success = true, message = "User activated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating user {userId}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Delete user
        /// </summary>
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                var result = await _userService.DeleteUserAsync(userId);

                if (!result)
                    return BadRequest(new { success = false, message = "Cannot delete user. User may be an admin or have active bookings." });

                _logger.LogInformation($"User {userId} deleted by admin");

                return Ok(new { success = true, message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting user {userId}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get user statistics
        /// </summary>
        [HttpGet("users/stats")]
        public async Task<IActionResult> GetUserStats()
        {
            try
            {
                var stats = await _userService.GetUserStatsAsync();
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user stats");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        // ===== BOOKING RULES =====

        /// <summary>
        /// Send trip reminders (can be called by a scheduled job)
        /// </summary>
        [HttpPost("send-reminders")]
        public async Task<IActionResult> SendTripReminders()
        {
            try
            {
                await _bookingService.SendTripRemindersAsync();
                _logger.LogInformation("Trip reminders sent");
                return Ok(new { success = true, message = "Trip reminders sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending trip reminders");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
    }

    public class SuspendUserRequest
    {
        public string? Reason { get; set; }
    }

    public class UpdateUserRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public int? Role { get; set; }      // 0 = Admin, 1 = User
        public int? Status { get; set; }    // 0 = Active, 1 = Inactive, 2 = Suspended, 3 = Deleted
    }
}