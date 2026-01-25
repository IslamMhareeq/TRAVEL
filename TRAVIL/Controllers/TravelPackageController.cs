using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Models;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/packages")]
    [Produces("application/json")]
    public class TravelPackageController : ControllerBase
    {
        private readonly ITravelPackageService _packageService;
        private readonly IBookingService _bookingService; // Added for waiting list processing
        private readonly ILogger<TravelPackageController> _logger;

        public TravelPackageController(
            ITravelPackageService packageService,
            IBookingService bookingService, // Inject booking service
            ILogger<TravelPackageController> logger)
        {
            _packageService = packageService;
            _bookingService = bookingService;
            _logger = logger;
        }

        /// <summary>
        /// Helper method to map TravelPackage to DTO (avoids circular references)
        /// </summary>
        private object MapPackageToDto(TravelPackage p)
        {
            return new
            {
                packageId = p.PackageId,
                destination = p.Destination,
                country = p.Country,
                description = p.Description,
                itinerary = p.Itinerary,
                price = p.Price,
                discountedPrice = p.DiscountedPrice,
                discountStartDate = p.DiscountStartDate,
                discountEndDate = p.DiscountEndDate,
                startDate = p.StartDate,
                endDate = p.EndDate,
                availableRooms = p.AvailableRooms,
                minimumAge = p.MinimumAge,
                maximumAge = p.MaximumAge,
                packageType = p.PackageType,
                isActive = p.IsActive,
                imageUrl = p.ImageUrl,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt,
                images = p.Images?.Select(img => new
                {
                    imageId = img.ImageId,
                    imageUrl = img.ImageUrl,
                    altText = img.AltText,
                    displayOrder = img.DisplayOrder
                }).ToList(),
                reviews = p.Reviews?.Where(r => r.IsApproved).Select(r => new
                {
                    reviewId = r.ReviewId,
                    userId = r.UserId,
                    rating = r.Rating,
                    comment = r.Comment,
                    createdAt = r.CreatedAt,
                    isApproved = r.IsApproved
                }).ToList(),
                bookingCount = p.Bookings?.Count ?? 0
            };
        }

        /// <summary>
        /// Get all packages (public)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllPackages()
        {
            var packages = await _packageService.GetActivePackagesAsync();
            var result = packages.Select(MapPackageToDto).ToList();
            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get all packages including inactive (Admin only)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllPackagesAdmin()
        {
            var packages = await _packageService.GetAllPackagesAsync();
            var result = packages.Select(MapPackageToDto).ToList();
            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get package by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPackage(int id)
        {
            var package = await _packageService.GetPackageByIdAsync(id);
            if (package == null)
                return NotFound(new { success = false, message = "Package not found" });

            return Ok(new { success = true, data = MapPackageToDto(package) });
        }

        /// <summary>
        /// Search packages with filters
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchPackages(
            [FromQuery] string destination,
            [FromQuery] string country,
            [FromQuery] PackageType? type,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string sortBy,
            [FromQuery] bool sortDesc = false)
        {
            var criteria = new PackageSearchCriteria
            {
                Destination = destination,
                Country = country,
                PackageType = type,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                StartDate = startDate,
                EndDate = endDate,
                SortBy = sortBy,
                SortDescending = sortDesc
            };

            var packages = await _packageService.SearchPackagesAsync(criteria);
            var result = packages.Select(MapPackageToDto).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get discounted packages
        /// </summary>
        [HttpGet("discounted")]
        public async Task<IActionResult> GetDiscountedPackages()
        {
            var packages = await _packageService.GetDiscountedPackagesAsync();
            var result = packages.Select(MapPackageToDto).ToList();
            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get popular packages
        /// </summary>
        [HttpGet("popular")]
        public async Task<IActionResult> GetPopularPackages([FromQuery] int count = 10)
        {
            var packages = await _packageService.GetPopularPackagesAsync(count);
            var result = packages.Select(MapPackageToDto).ToList();
            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Create new package (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreatePackage([FromBody] TravelPackageDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data", errors = ModelState });

            try
            {
                var package = await _packageService.CreatePackageAsync(dto);
                _logger.LogInformation($"Package created: {package.Destination}");

                return CreatedAtAction(nameof(GetPackage),
                    new { id = package.PackageId },
                    new { success = true, message = "Package created successfully", data = MapPackageToDto(package) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating package");
                return StatusCode(500, new { success = false, message = "An error occurred while creating the package" });
            }
        }

        /// <summary>
        /// Update package (Admin only) - NOW PROCESSES WAITING LIST WHEN ROOMS INCREASE
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePackage(int id, [FromBody] TravelPackageDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data", errors = ModelState });

            try
            {
                // Use the new method that tracks room changes
                var (updated, roomsIncreased, previousRooms) = await _packageService.UpdatePackageWithRoomTrackingAsync(id, dto);

                if (updated == null)
                    return NotFound(new { success = false, message = "Package not found" });

                // **KEY FIX: Process waiting list if rooms were increased**
                if (roomsIncreased && updated.AvailableRooms > 0)
                {
                    _logger.LogInformation($"Rooms increased for package {id} from {previousRooms} to {updated.AvailableRooms}. Processing waiting list...");

                    // Process waiting list - this will send email notifications to users in the queue
                    await _bookingService.ProcessWaitingListAsync(id);

                    _logger.LogInformation($"Waiting list processed for package {id}");
                }

                return Ok(new
                {
                    success = true,
                    message = roomsIncreased
                        ? "Package updated successfully. Waiting list users have been notified."
                        : "Package updated successfully",
                    data = MapPackageToDto(updated),
                    waitingListProcessed = roomsIncreased
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating package {id}");
                return StatusCode(500, new { success = false, message = "An error occurred while updating the package" });
            }
        }

        /// <summary>
        /// Delete package (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePackage(int id)
        {
            try
            {
                var result = await _packageService.DeletePackageAsync(id);
                if (!result)
                    return NotFound(new { success = false, message = "Package not found or has active bookings" });

                _logger.LogInformation($"Package deleted: {id}");

                return Ok(new { success = true, message = "Package deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting package {id}");
                return StatusCode(500, new { success = false, message = "An error occurred while deleting the package" });
            }
        }

        /// <summary>
        /// Toggle package active status (Admin only)
        /// </summary>
        [HttpPost("{id}/toggle-active")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var result = await _packageService.TogglePackageStatusAsync(id);
            if (!result)
                return NotFound(new { success = false, message = "Package not found" });

            var package = await _packageService.GetPackageByIdAsync(id);
            _logger.LogInformation($"Package {id} active status toggled to {package?.IsActive}");

            return Ok(new { success = true, message = $"Package {(package?.IsActive == true ? "activated" : "deactivated")} successfully" });
        }

        /// <summary>
        /// Apply discount to package (Admin only)
        /// </summary>
        [HttpPost("{id}/discount")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApplyDiscount(int id, [FromBody] ApplyDiscountRequest request)
        {
            if (request.DiscountedPrice <= 0)
                return BadRequest(new { success = false, message = "Discounted price must be greater than 0" });

            var package = await _packageService.GetPackageByIdAsync(id);
            if (package == null)
                return NotFound(new { success = false, message = "Package not found" });

            if (request.DiscountedPrice >= package.Price)
                return BadRequest(new { success = false, message = "Discounted price must be less than original price" });

            var result = await _packageService.ApplyDiscountAsync(id, request.DiscountedPrice, request.StartDate, request.EndDate);

            if (!result)
                return StatusCode(500, new { success = false, message = "Failed to apply discount" });

            // Check if discount duration exceeds 7 days
            var duration = (request.EndDate - request.StartDate).TotalDays;
            if (duration > 7)
                _logger.LogWarning($"Discount duration for package {id} exceeds 7 days: {duration} days");

            if (duration > 7)
                return Ok(new { success = true, message = "Discount applied successfully", warning = "Discount duration may exceed 1 week." });

            _logger.LogInformation($"Discount applied to package {id}");

            var updatedPackage = await _packageService.GetPackageByIdAsync(id);
            return Ok(new { success = true, message = "Discount applied successfully", data = MapPackageToDto(updatedPackage) });
        }

        /// <summary>
        /// Remove discount from package (Admin only)
        /// </summary>
        [HttpDelete("{id}/discount")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveDiscount(int id)
        {
            var result = await _packageService.RemoveDiscountAsync(id);
            if (!result)
                return NotFound(new { success = false, message = "Package not found" });

            _logger.LogInformation($"Discount removed from package {id}");

            return Ok(new { success = true, message = "Discount removed successfully" });
        }

        /// <summary>
        /// Get package statistics (Admin only)
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPackageStats()
        {
            try
            {
                var stats = await _packageService.GetDashboardStatsAsync();
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting package stats");
                return StatusCode(500, new { success = false, message = "An error occurred while fetching statistics" });
            }
        }

        /// <summary>
        /// Manually trigger waiting list processing (Admin only)
        /// Useful when admin wants to manually notify waiting users
        /// </summary>
        [HttpPost("{id}/process-waiting-list")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ProcessWaitingList(int id)
        {
            try
            {
                var package = await _packageService.GetPackageByIdAsync(id);
                if (package == null)
                    return NotFound(new { success = false, message = "Package not found" });

                if (package.AvailableRooms <= 0)
                    return BadRequest(new { success = false, message = "No rooms available to process waiting list" });

                await _bookingService.ProcessWaitingListAsync(id);

                _logger.LogInformation($"Waiting list manually processed for package {id}");

                return Ok(new { success = true, message = "Waiting list processed. Users have been notified if eligible." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing waiting list for package {id}");
                return StatusCode(500, new { success = false, message = "An error occurred while processing the waiting list" });
            }
        }
    }

    /// <summary>
    /// Request model for applying discount
    /// </summary>
    public class ApplyDiscountRequest
    {
        public decimal DiscountedPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}