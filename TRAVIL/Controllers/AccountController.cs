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
        private readonly IEmailService _emailService;

        public AccountController(
            IAuthenticationService authService,
            TravelDbContext context,
            ILogger<AccountController> logger,
            IEmailService emailService)
        {
            _authService = authService;
            _context = context;
            _logger = logger;
            _emailService = emailService;
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
        /// Gets dashboard data for the current user
        /// </summary>
        [HttpGet("dashboard")]
        [Authorize]
        public async Task<IActionResult> GetDashboard()
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

            var totalBookings = await _context.Bookings.CountAsync(b => b.UserId == userId);
            var upcomingTrips = await _context.Bookings
                .Include(b => b.TravelPackage)
                .CountAsync(b => b.UserId == userId &&
                           b.Status == BookingStatus.Confirmed &&
                           b.TravelPackage != null &&
                           b.TravelPackage.StartDate > DateTime.UtcNow);

            var wishlistCount = await _context.Wishlists.CountAsync(w => w.UserId == userId);

            var totalSpent = await _context.Bookings
                .Where(b => b.UserId == userId && b.Status == BookingStatus.Confirmed)
                .SumAsync(b => b.TotalPrice);

            return Ok(new
            {
                success = true,
                totalBookings,
                upcomingTrips,
                wishlistCount,
                totalSpent,
                user = new
                {
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email
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

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            if (!string.IsNullOrEmpty(request.FirstName))
                user.FirstName = request.FirstName;
            if (!string.IsNullOrEmpty(request.LastName))
                user.LastName = request.LastName;
            if (!string.IsNullOrEmpty(request.PhoneNumber))
                user.PhoneNumber = request.PhoneNumber;
            if (!string.IsNullOrEmpty(request.Address))
                user.Address = request.Address;
            if (!string.IsNullOrEmpty(request.City))
                user.City = request.City;
            if (!string.IsNullOrEmpty(request.PostalCode))
                user.PostalCode = request.PostalCode;
            if (!string.IsNullOrEmpty(request.Country))
                user.Country = request.Country;

            user.UpdatedAt = DateTime.UtcNow;

            try
            {
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
        /// Changes the user's password
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

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { success = false, message = "Current password is incorrect" });
            }

            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 6)
            {
                return BadRequest(new { success = false, message = "New password must be at least 6 characters" });
            }

            try
            {
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

        /// <summary>
        /// Step 1: Send verification code to email
        /// </summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request?.Email))
            {
                return BadRequest(new { success = false, message = "Email is required" });
            }

            _logger.LogInformation($"Password reset requested for: {request.Email}");

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

                if (user == null)
                {
                    // Return success anyway to prevent email enumeration
                    return Ok(new
                    {
                        success = true,
                        message = "If an account exists with this email, a verification code has been sent."
                    });
                }

                // Generate 6-digit verification code
                var random = new Random();
                var verificationCode = random.Next(100000, 999999).ToString();

                // Store code and expiry (15 minutes)
                user.PasswordResetToken = verificationCode;
                user.PasswordResetExpiry = DateTime.UtcNow.AddMinutes(15);
                await _context.SaveChangesAsync();

                // Send verification code email
                try
                {
                    await SendVerificationCodeEmailAsync(user.Email, user.FirstName, verificationCode);
                    _logger.LogInformation($"Verification code sent to: {request.Email}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send verification code to: {request.Email}");
                    return StatusCode(500, new { success = false, message = "Failed to send verification code. Please try again." });
                }

                return Ok(new
                {
                    success = true,
                    message = "Verification code sent to your email.",
                    email = MaskEmail(user.Email)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing forgot password for: {request.Email}");
                return StatusCode(500, new { success = false, message = "An error occurred. Please try again." });
            }
        }

        /// <summary>
        /// Step 2: Verify the code
        /// </summary>
        [HttpPost("verify-code")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
        {
            if (string.IsNullOrEmpty(request?.Email) || string.IsNullOrEmpty(request?.Code))
            {
                return BadRequest(new { success = false, message = "Email and code are required" });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == request.Email.ToLower() &&
                    u.PasswordResetToken == request.Code &&
                    u.PasswordResetExpiry > DateTime.UtcNow);

                if (user == null)
                {
                    return BadRequest(new { success = false, message = "Invalid or expired verification code" });
                }

                // Generate a temporary token for the reset step
                var resetToken = Guid.NewGuid().ToString("N");
                user.PasswordResetToken = resetToken;
                user.PasswordResetExpiry = DateTime.UtcNow.AddMinutes(10); // 10 minutes to reset
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Verification code validated for: {request.Email}");

                return Ok(new
                {
                    success = true,
                    message = "Code verified successfully",
                    resetToken = resetToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying code for: {request.Email}");
                return StatusCode(500, new { success = false, message = "An error occurred. Please try again." });
            }
        }

        /// <summary>
        /// Step 3: Reset password with token
        /// </summary>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request?.Email) ||
                string.IsNullOrEmpty(request?.ResetToken) ||
                string.IsNullOrEmpty(request?.NewPassword))
            {
                return BadRequest(new { success = false, message = "Email, reset token, and new password are required" });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { success = false, message = "Password must be at least 6 characters" });
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                return BadRequest(new { success = false, message = "Passwords do not match" });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == request.Email.ToLower() &&
                    u.PasswordResetToken == request.ResetToken &&
                    u.PasswordResetExpiry > DateTime.UtcNow);

                if (user == null)
                {
                    return BadRequest(new { success = false, message = "Invalid or expired reset token. Please start over." });
                }

                // Update password
                user.PasswordHash = HashPassword(request.NewPassword);
                user.PasswordResetToken = null;
                user.PasswordResetExpiry = null;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Password reset successfully for user: {user.Email}");

                return Ok(new { success = true, message = "Password has been reset successfully. You can now login." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(500, new { success = false, message = "An error occurred. Please try again." });
            }
        }

        /// <summary>
        /// Resend verification code
        /// </summary>
        [HttpPost("resend-code")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendCode([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request?.Email))
            {
                return BadRequest(new { success = false, message = "Email is required" });
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

                if (user == null)
                {
                    return Ok(new { success = true, message = "If an account exists, a new code has been sent." });
                }

                // Generate new 6-digit code
                var random = new Random();
                var verificationCode = random.Next(100000, 999999).ToString();

                user.PasswordResetToken = verificationCode;
                user.PasswordResetExpiry = DateTime.UtcNow.AddMinutes(15);
                await _context.SaveChangesAsync();

                await SendVerificationCodeEmailAsync(user.Email, user.FirstName, verificationCode);

                _logger.LogInformation($"Verification code resent to: {request.Email}");

                return Ok(new
                {
                    success = true,
                    message = "New verification code sent to your email."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resending code for: {request.Email}");
                return StatusCode(500, new { success = false, message = "Failed to resend code. Please try again." });
            }
        }

        /// <summary>
        /// Deletes the current user's account
        /// </summary>
        [HttpDelete("delete")]
        [Authorize]
        public async Task<IActionResult> DeleteAccount()
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

            try
            {
                user.Status = UserStatus.Inactive;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                Response.Cookies.Delete("authToken");

                _logger.LogInformation($"Account deleted for user {userId}");
                return Ok(new { success = true, message = "Account deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting account for user {userId}");
                return StatusCode(500, new { success = false, message = "An error occurred while deleting account" });
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

        // ===== PRIVATE HELPER METHODS =====

        private string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2) return email;

            var name = parts[0];
            var domain = parts[1];

            if (name.Length <= 2)
                return $"{name[0]}***@{domain}";

            return $"{name[0]}{name[1]}***@{domain}";
        }

        // Uses same parameters as AuthenticationService: 10000 iterations, 20 byte hash
        private string HashPassword(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using (var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
                password, salt, 10000, System.Security.Cryptography.HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(20);
                byte[] hashBytes = new byte[36];
                Buffer.BlockCopy(salt, 0, hashBytes, 0, 16);
                Buffer.BlockCopy(hash, 0, hashBytes, 16, 20);
                return Convert.ToBase64String(hashBytes);
            }
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                byte[] hashBytes = Convert.FromBase64String(storedHash);

                if (hashBytes.Length != 36)
                    return false;

                byte[] salt = new byte[16];
                Buffer.BlockCopy(hashBytes, 0, salt, 0, 16);

                using (var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
                    password, salt, 10000, System.Security.Cryptography.HashAlgorithmName.SHA256))
                {
                    byte[] hash = pbkdf2.GetBytes(20);
                    for (int i = 0; i < 20; i++)
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

        private async Task SendVerificationCodeEmailAsync(string toEmail, string userName, string code)
        {
            var subject = "Your TRAVIL Password Reset Code";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 0; padding: 0; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background: #ffffff; }}
        .header {{ background: linear-gradient(135deg, #0d1117, #161b22); color: #ffffff; padding: 40px 30px; text-align: center; }}
        .header h1 {{ margin: 0; color: #d4a853; font-size: 28px; letter-spacing: 2px; }}
        .content {{ padding: 40px 30px; text-align: center; }}
        .content h2 {{ color: #0d1117; margin-top: 0; }}
        .content p {{ color: #555; line-height: 1.6; }}
        .code-box {{ background: linear-gradient(135deg, #0d1117, #161b22); color: #d4a853; font-size: 36px; font-weight: bold; letter-spacing: 8px; padding: 20px 40px; border-radius: 12px; display: inline-block; margin: 25px 0; font-family: 'Courier New', monospace; }}
        .warning {{ background: #fff3cd; border: 1px solid #ffc107; border-radius: 8px; padding: 15px; margin-top: 20px; color: #856404; font-size: 14px; }}
        .footer {{ background: #f8f9fa; padding: 20px 30px; text-align: center; color: #888; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✈ TRAVIL</h1>
        </div>
        <div class='content'>
            <h2>Password Reset Code</h2>
            <p>Hi {userName},</p>
            <p>You requested to reset your password. Use the verification code below:</p>
            <div class='code-box'>{code}</div>
            <p>Enter this code on the password reset page to continue.</p>
            <div class='warning'>
                <strong>⚠️ This code expires in 15 minutes.</strong><br>
                If you didn't request this, please ignore this email.
            </div>
        </div>
        <div class='footer'>
            <p>© 2024 TRAVIL. All rights reserved.</p>
            <p>This is an automated message, please do not reply.</p>
        </div>
    </div>
</body>
</html>";

            await _emailService.SendEmailAsync(toEmail, subject, body);
        }
    }

    // ===== DTOs =====

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

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class VerifyCodeRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string ResetToken { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}