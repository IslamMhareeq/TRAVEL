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
            try
            {
                _logger.LogInformation($"Getting wishlist for user {userId}");

                var wishlist = await _context.Wishlists
                    .Include(w => w.TravelPackage)
                    .Where(w => w.UserId == userId)
                    .OrderByDescending(w => w.DateAdded)
                    .ToListAsync();

                _logger.LogInformation($"Found {wishlist.Count} items in wishlist for user {userId}");
                return wishlist;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting wishlist for user {userId}");
                return new List<WishlistItem>();
            }
        }

        public async Task<WishlistResult> AddToWishlistAsync(int userId, int packageId)
        {
            try
            {
                _logger.LogInformation($"Adding package {packageId} to wishlist for user {userId}");

                // Check if user exists
                var userExists = await _context.Users.AnyAsync(u => u.UserId == userId);
                if (!userExists)
                {
                    _logger.LogWarning($"User {userId} not found");
                    return new WishlistResult { Success = false, Message = "User not found" };
                }

                // Check if package exists
                var package = await _context.TravelPackages.FindAsync(packageId);
                if (package == null)
                {
                    _logger.LogWarning($"Package {packageId} not found");
                    return new WishlistResult { Success = false, Message = "Package not found" };
                }

                // Check if already in wishlist
                var existing = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.PackageId == packageId);

                if (existing != null)
                {
                    _logger.LogInformation($"Package {packageId} already in wishlist for user {userId}");
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

                _logger.LogInformation($"Successfully added package {packageId} to wishlist for user {userId}");

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
                return new WishlistResult { Success = false, Message = "Failed to add to wishlist: " + ex.Message };
            }
        }

        public async Task<WishlistResult> RemoveFromWishlistAsync(int userId, int packageId)
        {
            try
            {
                _logger.LogInformation($"Removing package {packageId} from wishlist for user {userId}");

                var wishlistItem = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.PackageId == packageId);

                if (wishlistItem == null)
                {
                    _logger.LogWarning($"Item not found in wishlist for user {userId}, package {packageId}");
                    return new WishlistResult { Success = false, Message = "Item not found in wishlist" };
                }

                _context.Wishlists.Remove(wishlistItem);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully removed package {packageId} from wishlist for user {userId}");

                return new WishlistResult { Success = true, Message = "Removed from wishlist" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing package {packageId} from wishlist for user {userId}");
                return new WishlistResult { Success = false, Message = "Failed to remove from wishlist: " + ex.Message };
            }
        }

        public async Task<bool> IsInWishlistAsync(int userId, int packageId)
        {
            try
            {
                return await _context.Wishlists
                    .AnyAsync(w => w.UserId == userId && w.PackageId == packageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking wishlist for user {userId}, package {packageId}");
                return false;
            }
        }

        public async Task<int> GetWishlistCountAsync(int userId)
        {
            try
            {
                return await _context.Wishlists.CountAsync(w => w.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting wishlist count for user {userId}");
                return 0;
            }
        }
    }

    public class WishlistResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public WishlistItem? WishlistItem { get; set; }
    }
}