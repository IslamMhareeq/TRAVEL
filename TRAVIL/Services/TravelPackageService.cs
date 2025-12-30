using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRAVEL.Data;
using TRAVEL.Models;

namespace TRAVEL.Services
{
    public interface ITravelPackageService
    {
        Task<List<TravelPackage>> GetAllPackagesAsync();
        Task<List<TravelPackage>> GetActivePackagesAsync();
        Task<TravelPackage> GetPackageByIdAsync(int packageId);
        Task<TravelPackage> CreatePackageAsync(TravelPackageDto dto);
        Task<TravelPackage> UpdatePackageAsync(int packageId, TravelPackageDto dto);
        Task<bool> DeletePackageAsync(int packageId);
        Task<List<TravelPackage>> SearchPackagesAsync(PackageSearchCriteria criteria);
        Task<bool> ApplyDiscountAsync(int packageId, decimal discountedPrice, DateTime startDate, DateTime endDate);
        Task<bool> RemoveDiscountAsync(int packageId);
        Task<bool> TogglePackageStatusAsync(int packageId);  // ADDED: Missing method
        Task<DashboardStats> GetDashboardStatsAsync();
        Task<List<TravelPackage>> GetDiscountedPackagesAsync();
        Task<List<TravelPackage>> GetPopularPackagesAsync(int count = 10);
    }

    public class TravelPackageService : ITravelPackageService
    {
        private readonly TravelDbContext _context;
        private readonly ILogger<TravelPackageService> _logger;

        public TravelPackageService(TravelDbContext context, ILogger<TravelPackageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<TravelPackage>> GetAllPackagesAsync()
        {
            return await _context.TravelPackages
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<TravelPackage>> GetActivePackagesAsync()
        {
            return await _context.TravelPackages
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .Where(p => p.IsActive && p.StartDate > DateTime.UtcNow)
                .OrderBy(p => p.StartDate)
                .ToListAsync();
        }

        public async Task<TravelPackage> GetPackageByIdAsync(int packageId)
        {
            return await _context.TravelPackages
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .Include(p => p.Bookings)
                .FirstOrDefaultAsync(p => p.PackageId == packageId);
        }

        public async Task<TravelPackage> CreatePackageAsync(TravelPackageDto dto)
        {
            var package = new TravelPackage
            {
                Destination = dto.Destination,
                Country = dto.Country,
                StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc),
                Price = dto.Price,
                AvailableRooms = dto.AvailableRooms,
                PackageType = dto.PackageType,
                MinimumAge = dto.MinimumAge,
                MaximumAge = dto.MaximumAge,
                Description = dto.Description,
                Itinerary = dto.Itinerary,
                ImageUrl = dto.ImageUrl,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.TravelPackages.Add(package);
            await _context.SaveChangesAsync();

            // Add multiple images if provided
            if (dto.ImageUrls != null && dto.ImageUrls.Any())
            {
                int order = 0;
                foreach (var imageUrl in dto.ImageUrls)
                {
                    _context.PackageImages.Add(new PackageImage
                    {
                        PackageId = package.PackageId,
                        ImageUrl = imageUrl,
                        DisplayOrder = order++
                    });
                }
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation($"Created new package: {package.Destination} (ID: {package.PackageId})");
            return package;
        }

        public async Task<TravelPackage> UpdatePackageAsync(int packageId, TravelPackageDto dto)
        {
            var package = await _context.TravelPackages
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.PackageId == packageId);

            if (package == null)
                return null;

            package.Destination = dto.Destination;
            package.Country = dto.Country;
            package.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
            package.EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);
            package.Price = dto.Price;
            package.AvailableRooms = dto.AvailableRooms;
            package.PackageType = dto.PackageType;
            package.MinimumAge = dto.MinimumAge;
            package.MaximumAge = dto.MaximumAge;
            package.Description = dto.Description;
            package.Itinerary = dto.Itinerary;
            package.ImageUrl = dto.ImageUrl;
            package.IsActive = dto.IsActive;
            package.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Updated package: {package.Destination} (ID: {package.PackageId})");
            return package;
        }

        public async Task<bool> DeletePackageAsync(int packageId)
        {
            var package = await _context.TravelPackages
                .Include(p => p.Bookings)
                .FirstOrDefaultAsync(p => p.PackageId == packageId);

            if (package == null)
                return false;

            // Check for active bookings
            var activeBookings = package.Bookings.Any(b =>
                b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending);

            if (activeBookings)
            {
                _logger.LogWarning($"Cannot delete package {packageId} - has active bookings");
                return false;
            }

            _context.TravelPackages.Remove(package);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Deleted package: {package.Destination} (ID: {package.PackageId})");
            return true;
        }

        public async Task<List<TravelPackage>> SearchPackagesAsync(PackageSearchCriteria criteria)
        {
            var query = _context.TravelPackages
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .Include(p => p.Bookings)
                .Where(p => p.IsActive);

            // Search by name/destination
            if (!string.IsNullOrEmpty(criteria.SearchTerm))
            {
                var searchLower = criteria.SearchTerm.ToLower();
                query = query.Where(p =>
                    p.Destination.ToLower().Contains(searchLower) ||
                    p.Country.ToLower().Contains(searchLower) ||
                    p.Description.ToLower().Contains(searchLower));
            }

            // Filter by destination
            if (!string.IsNullOrEmpty(criteria.Destination))
            {
                query = query.Where(p => p.Destination.ToLower().Contains(criteria.Destination.ToLower()));
            }

            // Filter by country
            if (!string.IsNullOrEmpty(criteria.Country))
            {
                query = query.Where(p => p.Country.ToLower() == criteria.Country.ToLower());
            }

            // Filter by category/type
            if (criteria.PackageType.HasValue)
            {
                query = query.Where(p => p.PackageType == criteria.PackageType.Value);
            }

            // Filter by price range
            if (criteria.MinPrice.HasValue)
            {
                query = query.Where(p => (p.DiscountedPrice ?? p.Price) >= criteria.MinPrice.Value);
            }

            if (criteria.MaxPrice.HasValue)
            {
                query = query.Where(p => (p.DiscountedPrice ?? p.Price) <= criteria.MaxPrice.Value);
            }

            // Filter by date range
            if (criteria.StartDateFrom.HasValue)
            {
                query = query.Where(p => p.StartDate >= criteria.StartDateFrom.Value);
            }

            if (criteria.StartDateTo.HasValue)
            {
                query = query.Where(p => p.StartDate <= criteria.StartDateTo.Value);
            }

            // Filter by duration (PostgreSQL compatible)
            if (criteria.MinDuration.HasValue)
            {
                query = query.Where(p => (p.EndDate - p.StartDate).Days >= criteria.MinDuration.Value);
            }

            if (criteria.MaxDuration.HasValue)
            {
                query = query.Where(p => (p.EndDate - p.StartDate).Days <= criteria.MaxDuration.Value);
            }

            // Sorting
            query = criteria.SortBy?.ToLower() switch
            {
                "price_asc" => query.OrderBy(p => p.DiscountedPrice ?? p.Price),
                "price_desc" => query.OrderByDescending(p => p.DiscountedPrice ?? p.Price),
                "popularity" => query.OrderByDescending(p => p.Bookings.Count),
                "rating" => query.OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0),
                "date_asc" => query.OrderBy(p => p.StartDate),
                "date_desc" => query.OrderByDescending(p => p.StartDate),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                _ => query.OrderBy(p => p.StartDate)
            };

            return await query.ToListAsync();
        }

        public async Task<bool> ApplyDiscountAsync(int packageId, decimal discountedPrice, DateTime startDate, DateTime endDate)
        {
            var package = await _context.TravelPackages.FindAsync(packageId);
            if (package == null)
                return false;

            // Validate discount duration (max 1 week)
            var duration = endDate - startDate;
            if (duration.TotalDays > 7)
            {
                _logger.LogWarning($"Discount duration exceeds 1 week for package {packageId}");
                return false;
            }

            // Validate discounted price
            if (discountedPrice >= package.Price)
            {
                _logger.LogWarning($"Discounted price must be less than original price for package {packageId}");
                return false;
            }

            package.DiscountedPrice = discountedPrice;
            package.DiscountStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            package.DiscountEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
            package.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Applied discount to package {packageId}: {package.Price} -> {discountedPrice}");
            return true;
        }

        public async Task<bool> RemoveDiscountAsync(int packageId)
        {
            var package = await _context.TravelPackages.FindAsync(packageId);
            if (package == null)
                return false;

            package.DiscountedPrice = null;
            package.DiscountStartDate = null;
            package.DiscountEndDate = null;
            package.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Removed discount from package {packageId}");
            return true;
        }

        /// <summary>
        /// Toggle package active/inactive status
        /// </summary>
        public async Task<bool> TogglePackageStatusAsync(int packageId)
        {
            var package = await _context.TravelPackages.FindAsync(packageId);
            if (package == null)
                return false;

            package.IsActive = !package.IsActive;
            package.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Toggled package {packageId} status to: {(package.IsActive ? "Active" : "Inactive")}");
            return true;
        }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            var packages = await _context.TravelPackages.ToListAsync();
            var bookings = await _context.Bookings.ToListAsync();
            var users = await _context.Users.ToListAsync();

            return new DashboardStats
            {
                TotalPackages = packages.Count,
                ActivePackages = packages.Count(p => p.IsActive && p.StartDate > DateTime.UtcNow),
                TotalBookings = bookings.Count,
                ConfirmedBookings = bookings.Count(b => b.Status == BookingStatus.Confirmed),
                PendingBookings = bookings.Count(b => b.Status == BookingStatus.Pending),
                TotalUsers = users.Count,
                ActiveUsers = users.Count(u => u.Status == UserStatus.Active),
                FullyBookedPackages = packages.Count(p => p.AvailableRooms == 0 && p.IsActive),
                TotalRevenue = bookings.Where(b => b.Status == BookingStatus.Confirmed).Sum(b => b.TotalPrice),
                PackagesOnSale = packages.Count(p =>
                    p.DiscountedPrice.HasValue &&
                    p.DiscountStartDate <= DateTime.UtcNow &&
                    p.DiscountEndDate >= DateTime.UtcNow)
            };
        }

        public async Task<List<TravelPackage>> GetDiscountedPackagesAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.TravelPackages
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .Where(p => p.IsActive &&
                           p.DiscountedPrice.HasValue &&
                           p.DiscountStartDate <= now &&
                           p.DiscountEndDate >= now)
                .OrderByDescending(p => (p.Price - p.DiscountedPrice.Value) / p.Price)
                .ToListAsync();
        }

        public async Task<List<TravelPackage>> GetPopularPackagesAsync(int count = 10)
        {
            return await _context.TravelPackages
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .Include(p => p.Bookings)
                .Where(p => p.IsActive && p.StartDate > DateTime.UtcNow)
                .OrderByDescending(p => p.Bookings.Count)
                .ThenByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0)
                .Take(count)
                .ToListAsync();
        }
    }

    // ===== DTOs =====

    public class TravelPackageDto
    {
        public string Destination { get; set; }
        public string Country { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Price { get; set; }
        public int AvailableRooms { get; set; }
        public PackageType PackageType { get; set; }
        public int? MinimumAge { get; set; }
        public int? MaximumAge { get; set; }
        public string Description { get; set; }
        public string? Itinerary { get; set; }
        public string? ImageUrl { get; set; }  // Made optional
        public List<string>? ImageUrls { get; set; }  // Made optional
        public bool IsActive { get; set; } = true;
    }

    public class PackageSearchCriteria
    {
        public string SearchTerm { get; set; }
        public string Destination { get; set; }
        public string Country { get; set; }
        public PackageType? PackageType { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public DateTime? StartDateFrom { get; set; }
        public DateTime? StartDateTo { get; set; }
        public int? MinDuration { get; set; }
        public int? MaxDuration { get; set; }
        public string SortBy { get; set; }
    }

    public class DashboardStats
    {
        public int TotalPackages { get; set; }
        public int ActivePackages { get; set; }
        public int TotalBookings { get; set; }
        public int ConfirmedBookings { get; set; }
        public int PendingBookings { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int FullyBookedPackages { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PackagesOnSale { get; set; }
    }
}