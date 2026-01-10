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
    public interface ICartService
    {
        Task<CartModels> GetOrCreateCartAsync(int userId);
        Task<CartModels> GetCartAsync(int userId);
        Task<CartItem?> AddToCartAsync(int userId, int packageId, int quantity = 1, int numberOfGuests = 1, string? specialRequests = null);
        Task<bool> UpdateCartItemAsync(int userId, int cartItemId, int quantity, int? numberOfGuests = null);
        Task<bool> RemoveFromCartAsync(int userId, int cartItemId);
        Task<bool> ClearCartAsync(int userId);
        Task<int> GetCartItemCountAsync(int userId);
        Task<decimal> GetCartTotalAsync(int userId);
        Task<List<CartItem>> GetCartItemsAsync(int userId);
    }

    public class CartService : ICartService
    {
        private readonly TravelDbContext _context;
        private readonly ILogger<CartService> _logger;

        public CartService(TravelDbContext context, ILogger<CartService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CartModels> GetOrCreateCartAsync(int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.TravelPackage)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new CartModels
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created new cart for user {userId}");
            }

            return cart;
        }

        public async Task<CartModels> GetCartAsync(int userId)
        {
            return await GetOrCreateCartAsync(userId);
        }

        public async Task<CartItem?> AddToCartAsync(int userId, int packageId, int quantity = 1, int numberOfGuests = 1, string? specialRequests = null)
        {
            try
            {
                // Get or create cart
                var cart = await GetOrCreateCartAsync(userId);

                // Check if package exists
                var package = await _context.TravelPackages.FindAsync(packageId);
                if (package == null)
                {
                    _logger.LogWarning($"Package {packageId} not found");
                    return null;
                }

                if (!package.IsActive)
                {
                    _logger.LogWarning($"Package {packageId} is not active");
                    return null;
                }

                if (package.AvailableRooms < quantity)
                {
                    _logger.LogWarning($"Package {packageId} doesn't have enough rooms");
                    return null;
                }

                // Check if item already in cart
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cart.CartId && ci.PackageId == packageId);

                if (existingItem != null)
                {
                    // Update quantity
                    existingItem.Quantity += quantity;
                    if (existingItem.Quantity > package.AvailableRooms)
                        existingItem.Quantity = package.AvailableRooms;

                    existingItem.UpdatedAt = DateTime.UtcNow;

                    if (!string.IsNullOrEmpty(specialRequests))
                        existingItem.SpecialRequests = specialRequests;

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Updated cart item quantity for package {packageId}");
                    return existingItem;
                }

                // Calculate price
                var unitPrice = package.DiscountedPrice ?? package.Price;

                // Add new item
                var cartItem = new CartItem
                {
                    CartId = cart.CartId,
                    PackageId = packageId,
                    Quantity = quantity,
                    NumberOfGuests = numberOfGuests,
                    UnitPrice = unitPrice,
                    SpecialRequests = specialRequests,
                    DateAdded = DateTime.UtcNow
                };

                _context.CartItems.Add(cartItem);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Added package {packageId} to cart for user {userId}");
                return cartItem;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding package {packageId} to cart for user {userId}");
                return null;
            }
        }

        public async Task<bool> UpdateCartItemAsync(int userId, int cartItemId, int quantity, int? numberOfGuests = null)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);

                var cartItem = await _context.CartItems
                    .Include(ci => ci.TravelPackage)
                    .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.CartId == cart.CartId);

                if (cartItem == null)
                {
                    _logger.LogWarning($"Cart item {cartItemId} not found");
                    return false;
                }

                if (quantity <= 0)
                {
                    _context.CartItems.Remove(cartItem);
                }
                else
                {
                    if (quantity > cartItem.TravelPackage?.AvailableRooms)
                    {
                        _logger.LogWarning($"Not enough rooms available for package {cartItem.PackageId}");
                        return false;
                    }

                    cartItem.Quantity = quantity;
                    if (numberOfGuests.HasValue)
                        cartItem.NumberOfGuests = numberOfGuests.Value;
                    cartItem.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating cart item {cartItemId}");
                return false;
            }
        }

        public async Task<bool> RemoveFromCartAsync(int userId, int cartItemId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);

                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.CartId == cart.CartId);

                if (cartItem == null)
                {
                    _logger.LogWarning($"Cart item {cartItemId} not found");
                    return false;
                }

                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Removed cart item {cartItemId} for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing cart item {cartItemId}");
                return false;
            }
        }

        public async Task<bool> ClearCartAsync(int userId)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null || !cart.Items.Any())
                    return true;

                _context.CartItems.RemoveRange(cart.Items);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Cleared cart for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error clearing cart for user {userId}");
                return false;
            }
        }

        public async Task<int> GetCartItemCountAsync(int userId)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                return cart?.Items?.Sum(i => i.Quantity) ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting cart count for user {userId}");
                return 0;
            }
        }

        public async Task<decimal> GetCartTotalAsync(int userId)
        {
            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                return cart?.Items?.Sum(i => i.Subtotal) ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting cart total for user {userId}");
                return 0;
            }
        }

        public async Task<List<CartItem>> GetCartItemsAsync(int userId)
        {
            try
            {
                var cart = await GetOrCreateCartAsync(userId);
                return cart.Items.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting cart items for user {userId}");
                return new List<CartItem>();
            }
        }
    }
}