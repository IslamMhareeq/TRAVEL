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
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly ILogger<ReviewController> _logger;

        public ReviewController(
            IReviewService reviewService,
            ILogger<ReviewController> logger)
        {
            _reviewService = reviewService;
            _logger = logger;
        }

        /// <summary>
        /// Create a review for a package
        /// </summary>
        [HttpPost("package")]
        [Authorize]
        public async Task<IActionResult> CreatePackageReview([FromBody] CreateReviewRequest request)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest(new { success = false, message = "Rating must be between 1 and 5" });

            if (string.IsNullOrWhiteSpace(request.Comment))
                return BadRequest(new { success = false, message = "Comment is required" });

            var result = await _reviewService.CreatePackageReviewAsync(
                userId, request.PackageId, request.Rating, request.Comment);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            _logger.LogInformation($"Package review created by user {userId} for package {request.PackageId}");

            return Ok(new
            {
                success = true,
                message = result.Message,
                reviewId = result.Review?.ReviewId
            });
        }

        /// <summary>
        /// Create a site review
        /// </summary>
        [HttpPost("site")]
        [Authorize]
        public async Task<IActionResult> CreateSiteReview([FromBody] CreateSiteReviewRequest request)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest(new { success = false, message = "Rating must be between 1 and 5" });

            if (string.IsNullOrWhiteSpace(request.Comment))
                return BadRequest(new { success = false, message = "Comment is required" });

            var result = await _reviewService.CreateSiteReviewAsync(userId, request.Rating, request.Comment);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            _logger.LogInformation($"Site review created by user {userId}");

            return Ok(new
            {
                success = true,
                message = result.Message,
                reviewId = result.Review?.ReviewId
            });
        }

        /// <summary>
        /// Get reviews for a package
        /// </summary>
        [HttpGet("package/{packageId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPackageReviews(int packageId)
        {
            var reviews = await _reviewService.GetPackageReviewsAsync(packageId, true);

            var result = reviews.Select(r => new
            {
                reviewId = r.ReviewId,
                userName = $"{r.User?.FirstName} {r.User?.LastName?.Substring(0, 1)}.",
                rating = r.Rating,
                comment = r.Comment,
                createdAt = r.CreatedAt
            }).ToList();

            var avgRating = await _reviewService.GetPackageAverageRatingAsync(packageId);

            return Ok(new
            {
                success = true,
                data = result,
                count = result.Count,
                averageRating = System.Math.Round(avgRating, 1)
            });
        }

        /// <summary>
        /// Get site reviews (for homepage)
        /// </summary>
        [HttpGet("site")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSiteReviews()
        {
            var reviews = await _reviewService.GetSiteReviewsAsync(true);

            var result = reviews.Select(r => new
            {
                reviewId = r.ReviewId,
                userName = $"{r.User?.FirstName} {r.User?.LastName?.Substring(0, 1)}.",
                rating = r.Rating,
                comment = r.Comment,
                createdAt = r.CreatedAt
            }).ToList();

            var avgRating = await _reviewService.GetSiteAverageRatingAsync();

            return Ok(new
            {
                success = true,
                data = result,
                count = result.Count,
                averageRating = System.Math.Round(avgRating, 1)
            });
        }

        /// <summary>
        /// Get user's reviews
        /// </summary>
        [HttpGet("my-reviews")]
        [Authorize]
        public async Task<IActionResult> GetMyReviews()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var reviews = await _reviewService.GetUserReviewsAsync(userId);

            var result = reviews.Select(r => new
            {
                reviewId = r.ReviewId,
                packageId = r.PackageId,
                packageName = r.TravelPackage?.Destination,
                rating = r.Rating,
                comment = r.Comment,
                reviewType = r.ReviewType,
                isApproved = r.IsApproved,
                createdAt = r.CreatedAt
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Check if user can review a package
        /// </summary>
        [HttpGet("can-review/{packageId}")]
        [Authorize]
        public async Task<IActionResult> CanReviewPackage(int packageId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var canReview = await _reviewService.CanUserReviewPackageAsync(userId, packageId);

            return Ok(new { success = true, canReview = canReview });
        }

        // ===== ADMIN ENDPOINTS =====

        /// <summary>
        /// Get pending reviews (Admin only)
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPendingReviews()
        {
            var reviews = await _reviewService.GetPendingReviewsAsync();

            var result = reviews.Select(r => new
            {
                reviewId = r.ReviewId,
                userId = r.UserId,
                userName = $"{r.User?.FirstName} {r.User?.LastName}",
                userEmail = r.User?.Email,
                packageId = r.PackageId,
                packageName = r.TravelPackage?.Destination,
                rating = r.Rating,
                comment = r.Comment,
                reviewType = r.ReviewType,
                createdAt = r.CreatedAt
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Approve a review (Admin only)
        /// </summary>
        [HttpPost("{reviewId}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveReview(int reviewId)
        {
            var result = await _reviewService.ApproveReviewAsync(reviewId);

            if (!result)
                return NotFound(new { success = false, message = "Review not found" });

            _logger.LogInformation($"Review {reviewId} approved");

            return Ok(new { success = true, message = "Review approved successfully" });
        }

        /// <summary>
        /// Delete a review (Admin only)
        /// </summary>
        [HttpDelete("{reviewId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            var result = await _reviewService.DeleteReviewAsync(reviewId);

            if (!result)
                return NotFound(new { success = false, message = "Review not found" });

            _logger.LogInformation($"Review {reviewId} deleted");

            return Ok(new { success = true, message = "Review deleted successfully" });
        }

        /// <summary>
        /// Get review statistics (Admin only)
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetReviewStats([FromQuery] int? packageId = null)
        {
            var stats = await _reviewService.GetReviewStatsAsync(packageId);

            return Ok(new { success = true, data = stats });
        }
    }

    public class CreateReviewRequest
    {
        public int PackageId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
    }

    public class CreateSiteReviewRequest
    {
        public int Rating { get; set; }
        public string Comment { get; set; }
    }
}
