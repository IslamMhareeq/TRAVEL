using System;
using System.Collections.Generic;
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
        /// Get all active packages
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetPackages()
        {
            var packages = await _packageService.GetActivePackagesAsync();
            return Ok(new { success = true, data = packages, count = packages.Count });
        }

        /// <summary>
        /// Get all packages (admin)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllPackages()
        {
            var packages = await _packageService.GetAllPackagesAsync();
            return Ok(new { success = true, data = packages, count = packages.Count });
        }

        /// <summary>
        /// Get package by ID
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
            }

            return Ok(new
            {
                success = true,
                data = package,
                averageRating = Math.Round(avgRating, 1),
                reviewCount = package.Reviews?.Count ?? 0,
                isOnSale = package.DiscountedPrice.HasValue &&
                          package.DiscountStartDate <= DateTime.UtcNow &&
                          package.DiscountEndDate >= DateTime.UtcNow
            });
        }

        /// <summary>
        /// Search packages with filters
        /// </summary>
        [HttpPost("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchPackages([FromBody] PackageSearchCriteria criteria)
        {
            var packages = await _packageService.SearchPackagesAsync(criteria);
            return Ok(new { success = true, data = packages, count = packages.Count });
        }

        /// <summary>
        /// Get discounted packages
        /// </summary>
        [HttpGet("on-sale")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDiscountedPackages()
        {
            var packages = await _packageService.GetDiscountedPackagesAsync();
            return Ok(new { success = true, data = packages, count = packages.Count });
        }

        /// <summary>
        /// Get popular packages
        /// </summary>
        [HttpGet("popular")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPopularPackages([FromQuery] int count = 10)
        {
            var packages = await _packageService.GetPopularPackagesAsync(count);
            return Ok(new { success = true, data = packages, count = packages.Count });
        }

        /// <summary>
        /// Get unique countries
        /// </summary>
        [HttpGet("countries")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCountries()
        {
            var packages = await _packageService.GetActivePackagesAsync();
            var countries = new HashSet<string>();
            foreach (var p in packages)
            {
                if (!string.IsNullOrEmpty(p.Country))
                    countries.Add(p.Country);
            }
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
            var destinations = new HashSet<string>();
            foreach (var p in packages)
            {
                if (!string.IsNullOrEmpty(p.Destination))
                    destinations.Add(p.Destination);
            }
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

                // Validate required fields
                if (string.IsNullOrEmpty(dto.Destination))
                    return BadRequest(new { success = false, message = "Destination is required" });

                if (string.IsNullOrEmpty(dto.Country))
                    return BadRequest(new { success = false, message = "Country is required" });

                // FIX: Changed from dto.StartDate <= DateTime.UtcNow to compare only dates
                // This allows today's date to be selected without timezone issues
                if (dto.StartDate.Date < DateTime.UtcNow.Date)
                    return BadRequest(new { success = false, message = "Start date cannot be in the past" });

                if (dto.EndDate <= dto.StartDate)
                    return BadRequest(new { success = false, message = "End date must be after start date" });

                if (dto.Price <= 0)
                    return BadRequest(new { success = false, message = "Price must be greater than 0" });

                if (dto.AvailableRooms <= 0)
                    return BadRequest(new { success = false, message = "Available rooms must be greater than 0" });

                if (string.IsNullOrEmpty(dto.Description))
                    return BadRequest(new { success = false, message = "Description is required" });

                var package = await _packageService.CreatePackageAsync(dto);

                _logger.LogInformation($"Package created: {package.Destination} by admin");

                return CreatedAtAction(nameof(GetPackage), new { id = package.PackageId },
                    new { success = true, message = "Package created successfully", data = package });
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
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid data", errors = ModelState });

                // Validate dates for updates as well
                if (dto.EndDate <= dto.StartDate)
                    return BadRequest(new { success = false, message = "End date must be after start date" });

                var package = await _packageService.UpdatePackageAsync(id, dto);
                if (package == null)
                    return NotFound(new { success = false, message = "Package not found" });

                _logger.LogInformation($"Package updated: {package.Destination} (ID: {id})");

                return Ok(new { success = true, message = "Package updated successfully", data = package });
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
                    return BadRequest(new { success = false, message = "Cannot delete package. It may have active bookings." });

                _logger.LogInformation($"Package deleted: ID {id}");

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
        [HttpPatch("{id}/toggle-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TogglePackageStatus(int id)
        {
            try
            {
                var package = await _packageService.GetPackageByIdAsync(id);
                if (package == null)
                    return NotFound(new { success = false, message = "Package not found" });

                var result = await _packageService.TogglePackageStatusAsync(id);
                if (!result)
                    return BadRequest(new { success = false, message = "Failed to toggle package status" });

                _logger.LogInformation($"Package status toggled: ID {id}");

                return Ok(new { success = true, message = "Package status updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling package status {id}");
                return StatusCode(500, new { success = false, message = "An error occurred while updating package status" });
            }
        }

        /// <summary>
        /// Apply discount to package (Admin only)
        /// </summary>
        [HttpPost("{id}/discount")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApplyDiscount(int id, [FromBody] ApplyDiscountRequest request)
        {
            try
            {
                if (request.DiscountedPrice <= 0)
                    return BadRequest(new { success = false, message = "Discounted price must be greater than 0" });

                if (request.StartDate >= request.EndDate)
                    return BadRequest(new { success = false, message = "End date must be after start date" });

                var result = await _packageService.ApplyDiscountAsync(
                    id,
                    request.DiscountedPrice,
                    request.StartDate,
                    request.EndDate);

                if (!result)
                    return BadRequest(new { success = false, message = "Failed to apply discount. Check that discounted price is less than original price and duration is max 7 days." });

                _logger.LogInformation($"Discount applied to package {id}");

                return Ok(new { success = true, message = "Discount applied successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error applying discount to package {id}");
                return StatusCode(500, new { success = false, message = "An error occurred while applying the discount" });
            }
        }

        /// <summary>
        /// Remove discount from package (Admin only)
        /// </summary>
        [HttpDelete("{id}/discount")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveDiscount(int id)
        {
            try
            {
                var result = await _packageService.RemoveDiscountAsync(id);
                if (!result)
                    return NotFound(new { success = false, message = "Package not found or no discount to remove" });

                _logger.LogInformation($"Discount removed from package {id}");

                return Ok(new { success = true, message = "Discount removed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing discount from package {id}");
                return StatusCode(500, new { success = false, message = "An error occurred while removing the discount" });
            }
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
                var allPackages = await _packageService.GetAllPackagesAsync();
                var activePackages = allPackages.FindAll(p => p.IsActive);
                var inactivePackages = allPackages.FindAll(p => !p.IsActive);
                var discountedPackages = await _packageService.GetDiscountedPackagesAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalPackages = allPackages.Count,
                        activePackages = activePackages.Count,
                        inactivePackages = inactivePackages.Count,
                        discountedPackages = discountedPackages.Count
                    }
                });
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