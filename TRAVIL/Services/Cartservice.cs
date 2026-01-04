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
    /// <summary>
    /// Interface for cart service operations
    /// </summary>
    public interface ICartService
    {
        Task<Cart> GetOrCreateCartAsync(int userId);
        Task<Cart?> GetCartByIdAsync(int cartId);
        Task<Cart?> GetActiveCartAsync(int userId);
        Task<CartResult> AddToCartAsync(int userId, int packageId, int quantity = 1, int guests = 1, string? specialRequests = null);
        Task<CartResult> UpdateCartItemAsync(int userId, int cartItemId, int quantity, int guests);
        Task<CartResult> RemoveFromCartAsync(int userId, int cartItemId);
        Task<CartResult> ClearCartAsync(int userId);
        Task<int> GetCartItemCountAsync(int userId);
        Task<decimal> GetCartTotalAsync(int userId);
        Task<bool> IsPackageInCartAsync(int userId, int packageId);
        Task<CartResult> CheckoutCartAsync(int userId);
    }

    /// <summary>
    /// Result class for cart operations
    /// </summary>
    public class CartResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Cart? Cart { get; set; }
        public CartItem? CartItem { get; set; }
        public List<Booking>? Bookings { get; set; }
    }

    /// <summary>
    /// Service for managing shopping cart operations
    /// </summary>
    public class CartService : ICartService
    {
        private readonly TravelDbContext _context;
        private readonly ILogger<CartService> _logger;
        private readonly IBookingService _bookingService;

        public CartService(
            TravelDbContext context,
            ILogger<CartService> logger,
            IBookingService bookingService)
        {
            _context = context;
            _logger = logger;
            _bookingService = bookingService;
        }

        /// <summary>
        /// Gets or creates an active cart for a user
        /// </summary>
        public async Task<Cart> GetOrCreateCartAsync(int userId)
        {
            try
            {
                _logger.LogInformation($"Getting or creating cart for user {userId}");

                var cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(i => i.TravelPackage)
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive);

                if (cart == null)
                {
                    _logger.LogInformation($"Creating new cart for user {userId}");
                    cart = new Cart
                    {
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    await _context.Carts.AddAsync(cart);
                    await _context.SaveChangesAsync();
                }

                return cart;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting or creating cart for user {userId}");
                throw;
            }
        }

        /// <summary>
        /// Gets a cart by its ID
        /// </summary>
        public async Task<Cart?> GetCartByIdAsync(int cartId)
        {
            return await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.TravelPackage)
                .FirstOrDefaultAsync(c => c.CartId == cartId);
        }

        /// <summary>
        /// Gets the active cart for a user
        /// </summary>
        public async Task<Cart?> GetActiveCartAsync(int userId)
        {
            return await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.TravelPackage)
                        .ThenInclude(p => p!.Images)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive);
        }

        /// <summary>
        /// Adds a package to the user's cart
        /// </summary>
        public async Task<CartResult> AddToCartAsync(
            int userId,
            int packageId,
            int quantity = 1,
            int guests = 1,
            string? specialRequests = null)
        {
            try
            {
                _logger.LogInformation($"Adding package {packageId} to cart for user {userId}");

                // Validate the package exists and is active
                var package = await _context.TravelPackages
                    .FirstOrDefaultAsync(p => p.PackageId == packageId && p.IsActive);

                if (package == null)
                {
                    return new CartResult
                    {
                        Success = false,
                        Message = "Package not found or is not available"
                    };
                }

                // Check available rooms
                if (package.AvailableRooms < quantity)
                {
                    return new CartResult
                    {
                        Success = false,
                        Message = $"Only {package.AvailableRooms} rooms available for this package"
                    };
                }

                // Get or create cart
                var cart = await GetOrCreateCartAsync(userId);

                // Check if package is already in cart
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartId == cart.CartId && ci.PackageId == packageId);

                if (existingItem != null)
                {
                    // Update existing item
                    existingItem.Quantity += quantity;
                    existingItem.NumberOfGuests = guests;
                    if (!string.IsNullOrEmpty(specialRequests))
                    {
                        existingItem.SpecialRequests = specialRequests;
                    }

                    if (existingItem.Quantity > package.AvailableRooms)
                    {
                        return new CartResult
                        {
                            Success = false,
                            Message = $"Cannot add more. Only {package.AvailableRooms} rooms available"
                        };
                    }
                }
                else
                {
                    // Create new cart item
                    var cartItem = new CartItem
                    {
                        CartId = cart.CartId,
                        PackageId = packageId,
                        Quantity = quantity,
                        NumberOfGuests = guests,
                        UnitPrice = package.DiscountedPrice ?? package.Price,
                        DateAdded = DateTime.UtcNow,
                        SpecialRequests = specialRequests
                    };

                    await _context.CartItems.AddAsync(cartItem);
                }

                // Update cart timestamp
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Reload cart with items
                var updatedCart = await GetActiveCartAsync(userId);

                _logger.LogInformation($"Package {packageId} added to cart for user {userId}");

                return new CartResult
                {
                    Success = true,
                    Message = "Package added to cart successfully",
                    Cart = updatedCart
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding package {packageId} to cart for user {userId}");
                return new CartResult
                {
                    Success = false,
                    Message = "An error occurred while adding to cart"
                };
            }
        }

        /// <summary>
        /// Updates a cart item's quantity and guest count
        /// </summary>
        public async Task<CartResult> UpdateCartItemAsync(int userId, int cartItemId, int quantity, int guests)
        {
            try
            {
                _logger.LogInformation($"Updating cart item {cartItemId} for user {userId}");

                var cart = await GetActiveCartAsync(userId);
                if (cart == null)
                {
                    return new CartResult { Success = false, Message = "Cart not found" };
                }

                var cartItem = await _context.CartItems
                    .Include(ci => ci.TravelPackage)
                    .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.CartId == cart.CartId);

                if (cartItem == null)
                {
                    return new CartResult { Success = false, Message = "Cart item not found" };
                }

                // Validate quantity against available rooms
                if (cartItem.TravelPackage != null && quantity > cartItem.TravelPackage.AvailableRooms)
                {
                    return new CartResult
                    {
                        Success = false,
                        Message = $"Only {cartItem.TravelPackage.AvailableRooms} rooms available"
                    };
                }

                if (quantity <= 0)
                {
                    // Remove item if quantity is 0 or less
                    _context.CartItems.Remove(cartItem);
                }
                else
                {
                    cartItem.Quantity = quantity;
                    cartItem.NumberOfGuests = guests;
                }

                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var updatedCart = await GetActiveCartAsync(userId);

                return new CartResult
                {
                    Success = true,
                    Message = "Cart item updated successfully",
                    Cart = updatedCart
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating cart item {cartItemId} for user {userId}");
                return new CartResult { Success = false, Message = "An error occurred while updating cart" };
            }
        }

        /// <summary>
        /// Removes an item from the cart
        /// </summary>
        public async Task<CartResult> RemoveFromCartAsync(int userId, int cartItemId)
        {
            try
            {
                _logger.LogInformation($"Removing cart item {cartItemId} for user {userId}");

                var cart = await GetActiveCartAsync(userId);
                if (cart == null)
                {
                    return new CartResult { Success = false, Message = "Cart not found" };
                }

                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.CartId == cart.CartId);

                if (cartItem == null)
                {
                    return new CartResult { Success = false, Message = "Cart item not found" };
                }

                _context.CartItems.Remove(cartItem);
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var updatedCart = await GetActiveCartAsync(userId);

                return new CartResult
                {
                    Success = true,
                    Message = "Item removed from cart",
                    Cart = updatedCart
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing cart item {cartItemId} for user {userId}");
                return new CartResult { Success = false, Message = "An error occurred while removing item" };
            }
        }

        /// <summary>
        /// Clears all items from a user's cart
        /// </summary>
        public async Task<CartResult> ClearCartAsync(int userId)
        {
            try
            {
                _logger.LogInformation($"Clearing cart for user {userId}");

                var cart = await GetActiveCartAsync(userId);
                if (cart == null)
                {
                    return new CartResult { Success = false, Message = "Cart not found" };
                }

                var cartItems = await _context.CartItems
                    .Where(ci => ci.CartId == cart.CartId)
                    .ToListAsync();

                _context.CartItems.RemoveRange(cartItems);
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new CartResult
                {
                    Success = true,
                    Message = "Cart cleared successfully",
                    Cart = cart
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error clearing cart for user {userId}");
                return new CartResult { Success = false, Message = "An error occurred while clearing cart" };
            }
        }

        /// <summary>
        /// Gets the number of items in a user's cart
        /// </summary>
        public async Task<int> GetCartItemCountAsync(int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive);

            return cart?.Items?.Sum(i => i.Quantity) ?? 0;
        }

        /// <summary>
        /// Gets the total price of items in a user's cart
        /// </summary>
        public async Task<decimal> GetCartTotalAsync(int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive);

            if (cart?.Items == null) return 0;

            return cart.Items.Sum(i => i.UnitPrice * i.Quantity);
        }

        /// <summary>
        /// Checks if a package is already in the user's cart
        /// </summary>
        public async Task<bool> IsPackageInCartAsync(int userId, int packageId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive);

            if (cart == null) return false;

            return cart.Items.Any(i => i.PackageId == packageId);
        }

        /// <summary>
        /// Processes checkout for all items in the cart
        /// </summary>
        public async Task<CartResult> CheckoutCartAsync(int userId)
        {
            try
            {
                _logger.LogInformation($"Processing checkout for user {userId}");

                var cart = await GetActiveCartAsync(userId);
                if (cart == null || !cart.Items.Any())
                {
                    return new CartResult { Success = false, Message = "Cart is empty" };
                }

                // Check user's active bookings count (max 3)
                var activeBookingsCount = await _context.Bookings
                    .CountAsync(b => b.UserId == userId &&
                        (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));

                if (activeBookingsCount + cart.Items.Count > 3)
                {
                    return new CartResult
                    {
                        Success = false,
                        Message = $"You can only have 3 active bookings. You currently have {activeBookingsCount}."
                    };
                }

                var bookings = new List<Booking>();

                // Create bookings for each cart item
                foreach (var item in cart.Items)
                {
                    // Validate package availability
                    var package = await _context.TravelPackages
                        .FirstOrDefaultAsync(p => p.PackageId == item.PackageId);

                    if (package == null || !package.IsActive)
                    {
                        return new CartResult
                        {
                            Success = false,
                            Message = $"Package '{item.TravelPackage?.Destination ?? "Unknown"}' is no longer available"
                        };
                    }

                    if (package.AvailableRooms < item.Quantity)
                    {
                        return new CartResult
                        {
                            Success = false,
                            Message = $"Not enough rooms available for '{package.Destination}'. Only {package.AvailableRooms} rooms left."
                        };
                    }

                    // Create booking
                    var booking = new Booking
                    {
                        UserId = userId,
                        PackageId = item.PackageId,
                        NumberOfRooms = item.Quantity,
                        NumberOfGuests = item.NumberOfGuests,
                        TotalPrice = item.UnitPrice * item.Quantity,
                        Status = BookingStatus.Pending,
                        BookingDate = DateTime.UtcNow,
                        BookingReference = GenerateBookingReference(),
                        SpecialRequests = item.SpecialRequests
                    };

                    // Reduce available rooms
                    package.AvailableRooms -= item.Quantity;

                    await _context.Bookings.AddAsync(booking);
                    bookings.Add(booking);
                }

                // Mark cart as inactive (checked out)
                cart.IsActive = false;
                cart.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Checkout completed for user {userId}, created {bookings.Count} bookings");

                return new CartResult
                {
                    Success = true,
                    Message = "Checkout successful! Proceed to payment.",
                    Bookings = bookings
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during checkout for user {userId}");
                return new CartResult
                {
                    Success = false,
                    Message = "An error occurred during checkout"
                };
            }
        }

        /// <summary>
        /// Generates a unique booking reference
        /// </summary>
        private string GenerateBookingReference()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"TRV-{timestamp}-{random}";
        }
    }
}