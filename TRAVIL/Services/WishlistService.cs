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
    public interface IWishlistService
    {
        Task<List<WishlistItem>> GetUserWishlistAsync(int userId);
        Task<WishlistResult> AddToWishlistAsync(int userId, int packageId);
        Task<WishlistResult> RemoveFromWishlistAsync(int userId, int packageId);
        Task<bool> IsInWishlistAsync(int userId, int packageId);
        Task<int> GetWishlistCountAsync(int userId);
    }

    public class WishlistService : IWishlistService
    {
        private readonly TravelDbContext _context;
        private readonly ILogger<WishlistService> _logger;

        public WishlistService(TravelDbContext context, ILogger<WishlistService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<WishlistItem>> GetUserWishlistAsync(int userId)
        {
            return await _context.Wishlists
                .Include(w => w.TravelPackage)
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.DateAdded)
                .ToListAsync();
        }

        public async Task<WishlistResult> AddToWishlistAsync(int userId, int packageId)
        {
            try
            {
                // Check if package exists
                var package = await _context.TravelPackages.FindAsync(packageId);
                if (package == null)
                {
                    return new WishlistResult { Success = false, Message = "Package not found" };
                }

                // Check if already in wishlist
                var existing = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.PackageId == packageId);

                if (existing != null)
                {
                    return new WishlistResult { Success = false, Message = "Package already in wishlist" };
                }

                var wishlistItem = new WishlistItem
                {
                    UserId = userId,
                    PackageId = packageId,
                    DateAdded = DateTime.UtcNow
                };

                _context.Wishlists.Add(wishlistItem);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} added package {packageId} to wishlist");

                return new WishlistResult
                {
                    Success = true,
                    Message = "Added to wishlist",
                    WishlistItem = wishlistItem
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding package {packageId} to wishlist for user {userId}");
                return new WishlistResult { Success = false, Message = "Failed to add to wishlist" };
            }
        }

        public async Task<WishlistResult> RemoveFromWishlistAsync(int userId, int packageId)
        {
            try
            {
                var wishlistItem = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.PackageId == packageId);

                if (wishlistItem == null)
                {
                    return new WishlistResult { Success = false, Message = "Item not found in wishlist" };
                }

                _context.Wishlists.Remove(wishlistItem);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} removed package {packageId} from wishlist");

                return new WishlistResult { Success = true, Message = "Removed from wishlist" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing package {packageId} from wishlist for user {userId}");
                return new WishlistResult { Success = false, Message = "Failed to remove from wishlist" };
            }
        }

        public async Task<bool> IsInWishlistAsync(int userId, int packageId)
        {
            return await _context.Wishlists
                .AnyAsync(w => w.UserId == userId && w.PackageId == packageId);
        }

        public async Task<int> GetWishlistCountAsync(int userId)
        {
            return await _context.Wishlists.CountAsync(w => w.UserId == userId);
        }
    }

    public class WishlistResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public WishlistItem? WishlistItem { get; set; }
    }
}