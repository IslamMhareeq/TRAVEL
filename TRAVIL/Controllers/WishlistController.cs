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
    public class WishlistController : ControllerBase
    {
        private readonly IWishlistService _wishlistService;
        private readonly ILogger<WishlistController> _logger;

        public WishlistController(
            IWishlistService wishlistService,
            ILogger<WishlistController> logger)
        {
            _wishlistService = wishlistService;
            _logger = logger;
        }

        private int? GetUserId()
        {
            // Try multiple claim types for UserId
            var userIdClaim = User.FindFirst("UserId")?.Value
                ?? User.FindFirst("userid")?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation($"Looking for UserId claim. Found: {userIdClaim}");

            // Log all claims for debugging
            foreach (var claim in User.Claims)
            {
                _logger.LogInformation($"Claim: {claim.Type} = {claim.Value}");
            }

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return null;
            }
            return userId;
        }

        /// <summary>
        /// Get user's wishlist
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetWishlist()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    _logger.LogWarning("GetWishlist: User not authenticated - no valid UserId claim found");
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                _logger.LogInformation($"GetWishlist for user {userId}");

                var wishlist = await _wishlistService.GetUserWishlistAsync(userId.Value);

                var result = wishlist.Select(w => new
                {
                    wishlistId = w.WishlistId,
                    packageId = w.PackageId,
                    dateAdded = w.DateAdded,
                    package = w.TravelPackage != null ? new
                    {
                        packageId = w.TravelPackage.PackageId,
                        destination = w.TravelPackage.Destination,
                        country = w.TravelPackage.Country,
                        price = w.TravelPackage.Price,
                        discountedPrice = w.TravelPackage.DiscountedPrice,
                        startDate = w.TravelPackage.StartDate,
                        endDate = w.TravelPackage.EndDate,
                        imageUrl = w.TravelPackage.ImageUrl,
                        isActive = w.TravelPackage.IsActive,
                        availableRooms = w.TravelPackage.AvailableRooms
                    } : null
                }).ToList();

                _logger.LogInformation($"Returning {result.Count} wishlist items for user {userId}");

                return Ok(new { success = true, data = result, count = result.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wishlist");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Add package to wishlist
        /// </summary>
        [HttpPost("{packageId}")]
        [Authorize]
        public async Task<IActionResult> AddToWishlist(int packageId)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    _logger.LogWarning("AddToWishlist: User not authenticated");
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                _logger.LogInformation($"AddToWishlist: User {userId} adding package {packageId}");

                var result = await _wishlistService.AddToWishlistAsync(userId.Value, packageId);

                if (!result.Success)
                {
                    _logger.LogWarning($"AddToWishlist failed: {result.Message}");
                    return BadRequest(new { success = false, message = result.Message });
                }

                _logger.LogInformation($"Package {packageId} added to wishlist for user {userId}");
                return Ok(new { success = true, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding package {packageId} to wishlist");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Remove package from wishlist
        /// </summary>
        [HttpDelete("{packageId}")]
        [Authorize]
        public async Task<IActionResult> RemoveFromWishlist(int packageId)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    _logger.LogWarning("RemoveFromWishlist: User not authenticated");
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                _logger.LogInformation($"RemoveFromWishlist: User {userId} removing package {packageId}");

                var result = await _wishlistService.RemoveFromWishlistAsync(userId.Value, packageId);

                if (!result.Success)
                {
                    _logger.LogWarning($"RemoveFromWishlist failed: {result.Message}");
                    return BadRequest(new { success = false, message = result.Message });
                }

                _logger.LogInformation($"Package {packageId} removed from wishlist for user {userId}");
                return Ok(new { success = true, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing package {packageId} from wishlist");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Check if package is in wishlist
        /// </summary>
        [HttpGet("check/{packageId}")]
        [Authorize]
        public async Task<IActionResult> CheckWishlist(int packageId)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var isInWishlist = await _wishlistService.IsInWishlistAsync(userId.Value, packageId);

                return Ok(new { success = true, inWishlist = isInWishlist });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking wishlist for package {packageId}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get wishlist count
        /// </summary>
        [HttpGet("count")]
        [Authorize]
        public async Task<IActionResult> GetWishlistCount()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var count = await _wishlistService.GetWishlistCountAsync(userId.Value);

                return Ok(new { success = true, count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wishlist count");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
    }
}