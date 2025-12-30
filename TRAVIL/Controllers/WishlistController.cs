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

        /// <summary>
        /// Get user's wishlist
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetWishlist()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var wishlist = await _wishlistService.GetUserWishlistAsync(userId);

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

            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Add package to wishlist
        /// </summary>
        [HttpPost("{packageId}")]
        [Authorize]
        public async Task<IActionResult> AddToWishlist(int packageId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _wishlistService.AddToWishlistAsync(userId, packageId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            _logger.LogInformation($"User {userId} added package {packageId} to wishlist");

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Remove package from wishlist
        /// </summary>
        [HttpDelete("{packageId}")]
        [Authorize]
        public async Task<IActionResult> RemoveFromWishlist(int packageId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _wishlistService.RemoveFromWishlistAsync(userId, packageId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            _logger.LogInformation($"User {userId} removed package {packageId} from wishlist");

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Check if package is in wishlist
        /// </summary>
        [HttpGet("check/{packageId}")]
        [Authorize]
        public async Task<IActionResult> CheckWishlist(int packageId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var isInWishlist = await _wishlistService.IsInWishlistAsync(userId, packageId);

            return Ok(new { success = true, inWishlist = isInWishlist });
        }

        /// <summary>
        /// Get wishlist count
        /// </summary>
        [HttpGet("count")]
        [Authorize]
        public async Task<IActionResult> GetWishlistCount()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var count = await _wishlistService.GetWishlistCountAsync(userId);

            return Ok(new { success = true, count = count });
        }
    }
}