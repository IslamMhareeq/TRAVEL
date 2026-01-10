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
        private readonly ILogger<TravelPackageController> _logger;

        public TravelPackageController(
            ITravelPackageService packageService,
            ILogger<TravelPackageController> logger)
        {
            _packageService = packageService;
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
                // Map images without back-reference
                images = p.Images?.Select(img => new
                {
                    imageId = img.ImageId,
                    imageUrl = img.ImageUrl,
                    altText = img.AltText,
                    displayOrder = img.DisplayOrder
                }).ToList(),
                // Map reviews without back-reference
                reviews = p.Reviews?.Where(r => r.IsApproved).Select(r => new
                {
                    reviewId = r.ReviewId,
                    userId = r.UserId,
                    rating = r.Rating,
                    comment = r.Comment,
                    createdAt = r.CreatedAt,
                    isApproved = r.IsApproved
                }).ToList(),
                // Count bookings instead of including them (to avoid circular reference)
                bookingCount = p.Bookings?.Count ?? 0
            };
        }

        /// <summary>
        /// Get all active packages
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetPackages()
        {
            var packages = await _packageService.GetActivePackagesAsync();
            var result = packages.Select(p => MapPackageToDto(p)).ToList();
            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get all packages (admin)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllPackages()
        {
            var packages = await _packageService.GetAllPackagesAsync();
            var result = packages.Select(p => MapPackageToDto(p)).ToList();
            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get package by ID - FIXED to return DTO without circular references
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPackage(int id)
        {
            var package = await _packageService.GetPackageByIdAsync(id);
            if (package == null)
                return NotFound(new { success = false, message = "Package not found" });

            // Calculate average rating
            double avgRating = 0;
            int reviewCount = 0;
            if (package.Reviews != null && package.Reviews.Count > 0)
            {
                double sum = 0;
                int count = 0;
                foreach (var review in package.Reviews)
                {
                    if (review.IsApproved)
                    {
                        sum += review.Rating;
                        count++;
                    }
                }
                if (count > 0)
                    avgRating = sum / count;
                reviewCount = count;
            }

            // Map to DTO to avoid circular reference
            var packageDto = MapPackageToDto(package);

            return Ok(new
            {
                success = true,
                data = packageDto,
                averageRating = Math.Round(avgRating, 1),
                reviewCount = reviewCount
            });
        }

        /// <summary>
        /// Get popular packages
        /// </summary>
        [HttpGet("popular")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPopularPackages([FromQuery] int limit = 6)
        {
            var packages = await _packageService.GetPopularPackagesAsync(limit);
            var result = packages.Select(p => MapPackageToDto(p)).ToList();
            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get discounted packages
        /// </summary>
        [HttpGet("discounted")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDiscountedPackages()
        {
            var packages = await _packageService.GetDiscountedPackagesAsync();
            var result = packages.Select(p => MapPackageToDto(p)).ToList();
            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Search packages using criteria object
        /// </summary>
        [HttpPost("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchPackages([FromBody] PackageSearchCriteria criteria)
        {
            var packages = await _packageService.SearchPackagesAsync(criteria);
            var result = packages.Select(p => MapPackageToDto(p)).ToList();
            return Ok(new { success = true, data = result, count = result.Count });
        }

        /// <summary>
        /// Get unique countries
        /// </summary>
        [HttpGet("countries")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCountries()
        {
            var packages = await _packageService.GetActivePackagesAsync();
            var countries = packages
                .Where(p => !string.IsNullOrEmpty(p.Country))
                .Select(p => p.Country)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            return Ok(new { success = true, data = countries });
        }

        /// <summary>
        /// Get unique destinations
        /// </summary>
        [HttpGet("destinations")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDestinations()
        {
            var packages = await _packageService.GetActivePackagesAsync();
            var destinations = packages
                .Where(p => !string.IsNullOrEmpty(p.Destination))
                .Select(p => p.Destination)
                .Distinct()
                .OrderBy(d => d)
                .ToList();
            return Ok(new { success = true, data = destinations });
        }

        /// <summary>
        /// Create new package (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreatePackage([FromBody] TravelPackageDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid data", errors = ModelState });

                if (string.IsNullOrEmpty(dto.Destination))
                    return BadRequest(new { success = false, message = "Destination is required" });

                if (string.IsNullOrEmpty(dto.Country))
                    return BadRequest(new { success = false, message = "Country is required" });

                if (dto.Price <= 0)
                    return BadRequest(new { success = false, message = "Price must be greater than zero" });

                // Ensure dates are in UTC
                dto.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
                dto.EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);

                if (dto.EndDate <= dto.StartDate)
                    return BadRequest(new { success = false, message = "End date must be after start date" });

                var createdPackage = await _packageService.CreatePackageAsync(dto);

                _logger.LogInformation($"Package created: {createdPackage.PackageId} - {createdPackage.Destination}");

                return CreatedAtAction(nameof(GetPackage), new { id = createdPackage.PackageId },
                    new { success = true, message = "Package created successfully", data = MapPackageToDto(createdPackage) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating package");
                return StatusCode(500, new { success = false, message = "An error occurred while creating the package" });
            }
        }

        /// <summary>
        /// Update package (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePackage(int id, [FromBody] TravelPackageDto dto)
        {
            try
            {
                var existing = await _packageService.GetPackageByIdAsync(id);
                if (existing == null)
                    return NotFound(new { success = false, message = "Package not found" });

                // Ensure dates are in UTC
                dto.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
                dto.EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);

                var updated = await _packageService.UpdatePackageAsync(id, dto);

                if (updated == null)
                    return NotFound(new { success = false, message = "Package not found" });

                _logger.LogInformation($"Package updated: {id}");

                return Ok(new { success = true, message = "Package updated successfully", data = MapPackageToDto(updated) });
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

            return Ok(new { success = true, message = $"Package {(package?.IsActive == true ? "activated" : "deactivated")} successfully", isActive = package?.IsActive });
        }

        /// <summary>
        /// Apply discount to package (Admin only)
        /// </summary>
        [HttpPost("{id}/discount")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApplyDiscount(int id, [FromBody] ApplyDiscountRequest request)
        {
            var package = await _packageService.GetPackageByIdAsync(id);
            if (package == null)
                return NotFound(new { success = false, message = "Package not found" });

            if (request.DiscountedPrice >= package.Price)
                return BadRequest(new { success = false, message = "Discounted price must be less than original price" });

            var startDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc);

            var result = await _packageService.ApplyDiscountAsync(id, request.DiscountedPrice, startDate, endDate);

            if (!result)
                return BadRequest(new { success = false, message = "Failed to apply discount. Discount duration may exceed 1 week." });

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