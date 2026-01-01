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
    public interface IUserManagementService
    {
        Task<List<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(int userId);
        Task<bool> UpdateUserAsync(User user);
        Task<bool> SuspendUserAsync(int userId, string reason);
        Task<bool> ActivateUserAsync(int userId);
        Task<bool> DeleteUserAsync(int userId);
        Task<UserBookingHistory?> GetUserBookingHistoryAsync(int userId);
        Task<List<User>> SearchUsersAsync(string searchTerm);
        Task<UserStats> GetUserStatsAsync();
    }

    public class UserManagementService : IUserManagementService
    {
        private readonly TravelDbContext _context;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(TravelDbContext context, ILogger<UserManagementService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .Include(u => u.Bookings)
                .Include(u => u.Reviews)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.TravelPackage)
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Payment)
                .Include(u => u.Reviews)
                .Include(u => u.WaitingListEntries)
                    .ThenInclude(w => w.TravelPackage)
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"User {user.UserId} updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user {user.UserId}");
                return false;
            }
        }

        public async Task<bool> SuspendUserAsync(int userId, string reason)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            if (user.Role == UserRole.Admin)
            {
                _logger.LogWarning($"Cannot suspend admin user {userId}");
                return false;
            }

            user.Status = UserStatus.Suspended;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {userId} suspended. Reason: {reason}");
            return true;
        }

        public async Task<bool> ActivateUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            user.Status = UserStatus.Active;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {userId} activated");
            return true;
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Bookings)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return false;

            if (user.Role == UserRole.Admin)
            {
                _logger.LogWarning($"Cannot delete admin user {userId}");
                return false;
            }

            // Check for active bookings
            var hasActiveBookings = user.Bookings.Any(b =>
                b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);

            if (hasActiveBookings)
            {
                // Soft delete instead
                user.Status = UserStatus.Deleted;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} soft deleted (has active bookings)");
                return true;
            }

            // Hard delete
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {userId} permanently deleted");
            return true;
        }

        public async Task<UserBookingHistory?> GetUserBookingHistoryAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.TravelPackage)
                .Include(u => u.Bookings)
                    .ThenInclude(b => b.Payment)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return null;

            return new UserBookingHistory
            {
                UserId = user.UserId,
                UserName = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                TotalBookings = user.Bookings.Count,
                CompletedBookings = user.Bookings.Count(b => b.Status == BookingStatus.Completed),
                CancelledBookings = user.Bookings.Count(b => b.Status == BookingStatus.Cancelled),
                TotalSpent = user.Bookings
                    .Where(b => b.Status == BookingStatus.Completed || b.Status == BookingStatus.Confirmed)
                    .Sum(b => b.TotalPrice),
                Bookings = user.Bookings.Select(b => new BookingHistoryItem
                {
                    BookingId = b.BookingId,
                    BookingReference = b.BookingReference ?? $"TRV-{b.BookingId:D6}",
                    Destination = b.TravelPackage?.Destination ?? "Unknown",
                    TravelDate = b.TravelPackage?.StartDate ?? DateTime.MinValue,
                    BookingDate = b.BookingDate,
                    Status = b.Status.ToString(),
                    Amount = b.TotalPrice,
                    PaymentStatus = b.Payment?.Status.ToString() ?? "N/A"
                }).OrderByDescending(b => b.BookingDate).ToList()
            };
        }

        public async Task<List<User>> SearchUsersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllUsersAsync();

            var term = searchTerm.ToLower();

            return await _context.Users
                .Include(u => u.Bookings)
                .Where(u =>
                    u.FirstName.ToLower().Contains(term) ||
                    u.LastName.ToLower().Contains(term) ||
                    u.Email.ToLower().Contains(term))
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserStats> GetUserStatsAsync()
        {
            var users = await _context.Users.ToListAsync();
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);

            return new UserStats
            {
                TotalUsers = users.Count,
                ActiveUsers = users.Count(u => u.Status == UserStatus.Active),
                SuspendedUsers = users.Count(u => u.Status == UserStatus.Suspended),
                DeletedUsers = users.Count(u => u.Status == UserStatus.Deleted),
                NewUsersThisMonth = users.Count(u => u.CreatedAt >= thirtyDaysAgo),
                UsersWithBookings = await _context.Users.CountAsync(u => u.Bookings.Any()),
                AdminCount = users.Count(u => u.Role == UserRole.Admin),
                RegularUserCount = users.Count(u => u.Role == UserRole.User)
            };
        }
    }

    public class UserBookingHistory
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int TotalBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int CancelledBookings { get; set; }
        public decimal TotalSpent { get; set; }
        public List<BookingHistoryItem> Bookings { get; set; } = new();
    }

    public class BookingHistoryItem
    {
        public int BookingId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateTime TravelDate { get; set; }
        public DateTime BookingDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class UserStats
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int SuspendedUsers { get; set; }
        public int DeletedUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int UsersWithBookings { get; set; }
        public int AdminCount { get; set; }
        public int RegularUserCount { get; set; }
    }
}