using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using TRAVEL.Models;
using TRAVEL.Services;
using TRAVEL.Data;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Account controller for user authentication and profile management
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AccountController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly TravelDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IAuthenticationService authService,
            TravelDbContext context,
            ILogger<AccountController> logger)
        {
            _authService = authService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Registers a new user account
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid input data", errors = ModelState });
            }

            _logger.LogInformation($"Registration attempt for email: {request.Email}");

            var result = await _authService.RegisterAsync(request);

            if (result.Success)
            {
                _logger.LogInformation($"User registered successfully: {request.Email}");
                return Ok(result);
            }

            _logger.LogWarning($"Registration failed for email: {request.Email}. Reason: {result.Message}");
            return BadRequest(result);
        }

        /// <summary>
        /// Authenticates a user and returns JWT token
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid input data", errors = ModelState });
            }

            _logger.LogInformation($"Login attempt for email: {request.Email}");

            var result = await _authService.LoginAsync(request);

            if (result.Success)
            {
                _logger.LogInformation($"User logged in successfully: {request.Email}");

                Response.Cookies.Append("authToken", result.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(60)
                });

                return Ok(result);
            }

            _logger.LogWarning($"Login failed for email: {request.Email}");
            return Unauthorized(result);
        }

        /// <summary>
        /// Logs out the current user
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            _logger.LogInformation($"User {userId} logged out");

            await _authService.LogoutAsync(userId);

            Response.Cookies.Delete("authToken");

            return Ok(new { success = true, message = "Logged out successfully" });
        }

        /// <summary>
        /// Verifies if the current token is valid
        /// </summary>
        [HttpGet("verify-token")]
        [Authorize]
        public async Task<IActionResult> VerifyToken()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var isValid = await _authService.ValidateTokenAsync(token);

            if (isValid)
            {
                var userId = User.FindFirst("UserId")?.Value;
                return Ok(new { success = true, message = "Token is valid", userId });
            }

            return Unauthorized(new { success = false, message = "Invalid token" });
        }

        /// <summary>
        /// Gets the current logged in user info
        /// </summary>
        [HttpGet("current-user")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { success = false, message = "User not authenticated" });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            return Ok(new
            {
                success = true,
                user = new
                {
                    userId = user.UserId,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    phoneNumber = user.PhoneNumber,
                    address = user.Address,
                    city = user.City,
                    postalCode = user.PostalCode,
                    country = user.Country,
                    role = (int)user.Role,
                    status = (int)user.Status,
                    createdAt = user.CreatedAt
                }
            });
        }

        /// <summary>
        /// Updates the current user's profile
        /// </summary>
        [HttpPut("update-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { success = false, message = "User not authenticated" });
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                // Update user fields
                if (!string.IsNullOrEmpty(request.FirstName))
                    user.FirstName = request.FirstName;

                if (!string.IsNullOrEmpty(request.LastName))
                    user.LastName = request.LastName;

                if (!string.IsNullOrEmpty(request.PhoneNumber))
                    user.PhoneNumber = request.PhoneNumber;

                if (request.Address != null)
                    user.Address = request.Address;

                if (request.City != null)
                    user.City = request.City;

                if (request.PostalCode != null)
                    user.PostalCode = request.PostalCode;

                if (request.Country != null)
                    user.Country = request.Country;

                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Profile updated for user {userId}");

                return Ok(new
                {
                    success = true,
                    message = "Profile updated successfully",
                    user = new
                    {
                        userId = user.UserId,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        email = user.Email,
                        phoneNumber = user.PhoneNumber,
                        address = user.Address,
                        city = user.City,
                        postalCode = user.PostalCode,
                        country = user.Country
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating profile for user {userId}");
                return StatusCode(500, new { success = false, message = "An error occurred while updating profile" });
            }
        }

        /// <summary>
        /// Changes the current user's password
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { success = false, message = "User not authenticated" });
            }

            if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
            {
                return BadRequest(new { success = false, message = "Current password and new password are required" });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { success = false, message = "New password must be at least 6 characters" });
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                // Verify current password using PBKDF2-SHA256
                var isValid = VerifyPassword(request.CurrentPassword, user.PasswordHash);
                if (!isValid)
                {
                    return BadRequest(new { success = false, message = "Current password is incorrect" });
                }

                // Hash new password using PBKDF2-SHA256
                user.PasswordHash = HashPassword(request.NewPassword);
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Password changed for user {userId}");

                return Ok(new { success = true, message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for user {userId}");
                return StatusCode(500, new { success = false, message = "An error occurred while changing password" });
            }
        }

        // Password hashing using PBKDF2-SHA256 (matches AuthenticationService)
        private string HashPassword(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using (var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 100000, System.Security.Cryptography.HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(32);
                byte[] hashBytes = new byte[48];
                Array.Copy(salt, 0, hashBytes, 0, 16);
                Array.Copy(hash, 0, hashBytes, 16, 32);
                return Convert.ToBase64String(hashBytes);
            }
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                byte[] hashBytes = Convert.FromBase64String(storedHash);
                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                using (var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 100000, System.Security.Cryptography.HashAlgorithmName.SHA256))
                {
                    byte[] hash = pbkdf2.GetBytes(32);
                    for (int i = 0; i < 32; i++)
                    {
                        if (hashBytes[i + 16] != hash[i])
                            return false;
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }

    // DTOs
    public class UpdateProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}