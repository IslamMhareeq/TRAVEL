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
    public interface IBookingService
    {
        Task<BookingResult> CreateBookingAsync(int userId, int packageId, int numberOfRooms, int numberOfGuests);
        Task<BookingResult> ConfirmBookingAsync(int bookingId);
        Task<BookingResult> CancelBookingAsync(int bookingId, string reason);
        Task<List<Booking>> GetUserBookingsAsync(int userId);
        Task<Booking> GetBookingByIdAsync(int bookingId);
        Task<List<Booking>> GetAllBookingsAsync();
        Task<int> GetActiveBookingCountAsync(int userId);
        Task<bool> CanUserBookAsync(int userId);

        // Waiting List
        Task<WaitingListResult> JoinWaitingListAsync(int userId, int packageId, int numberOfRooms);
        Task<bool> LeaveWaitingListAsync(int userId, int packageId);
        Task<List<WaitingListEntry>> GetWaitingListAsync(int packageId);
        Task<List<WaitingListEntry>> GetUserWaitingListAsync(int userId);
        Task<bool> IsUserNextInWaitingListAsync(int userId, int packageId);
        Task ProcessWaitingListAsync(int packageId);

        // Admin
        Task<List<Booking>> GetBookingsByPackageAsync(int packageId);
        Task SendTripRemindersAsync();
    }

    public class BookingService : IBookingService
    {
        private readonly TravelDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<BookingService> _logger;
        private const int MAX_ACTIVE_BOOKINGS = 3;

        public BookingService(
            TravelDbContext context,
            IEmailService emailService,
            ILogger<BookingService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<BookingResult> CreateBookingAsync(int userId, int packageId, int numberOfRooms, int numberOfGuests)
        {
            // Get user
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return new BookingResult { Success = false, Message = "User not found" };

            // Get package
            var package = await _context.TravelPackages
                .Include(p => p.WaitingList)
                .FirstOrDefaultAsync(p => p.PackageId == packageId);

            if (package == null)
                return new BookingResult { Success = false, Message = "Package not found" };

            // Check if package is active
            if (!package.IsActive)
                return new BookingResult { Success = false, Message = "This package is no longer available" };

            // Check if trip end date has already passed (more lenient - allow booking up until trip ends)
            if (package.EndDate < DateTime.UtcNow)
                return new BookingResult { Success = false, Message = "This trip has already ended" };

            // Check if trip has already started (optional - you can remove this for even more flexibility)
            if (package.StartDate < DateTime.UtcNow)
            {
                // Allow bookings for trips that started but haven't ended (late joiners)
                _logger.LogWarning($"Booking for already started trip: Package {packageId}");
            }

            // Check user's active bookings (max 3)
            var activeBookings = await GetActiveBookingCountAsync(userId);
            if (activeBookings >= MAX_ACTIVE_BOOKINGS)
                return new BookingResult { Success = false, Message = $"You cannot have more than {MAX_ACTIVE_BOOKINGS} active bookings" };

            // Check if user already has a booking for this package
            var existingBooking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.UserId == userId &&
                                         b.PackageId == packageId &&
                                         (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            if (existingBooking != null)
                return new BookingResult { Success = false, Message = "You already have a booking for this package" };

            // Check room availability
            if (package.AvailableRooms < numberOfRooms)
            {
                // Check if user is next in waiting list (if there's a waiting list)
                if (package.WaitingList.Any())
                {
                    var isNext = await IsUserNextInWaitingListAsync(userId, packageId);
                    if (!isNext)
                        return new BookingResult { Success = false, Message = "Package is fully booked. Please join the waiting list." };
                }
                else
                {
                    return new BookingResult { Success = false, Message = "Not enough rooms available" };
                }
            }

            // Calculate total price
            var unitPrice = package.DiscountedPrice ?? package.Price;
            var totalPrice = unitPrice * numberOfRooms;

            // Generate booking reference
            var bookingReference = GenerateBookingReference();

            // Create booking
            var booking = new Booking
            {
                UserId = userId,
                PackageId = packageId,
                NumberOfRooms = numberOfRooms,
                NumberOfGuests = numberOfGuests,
                Status = BookingStatus.Pending,
                TotalPrice = totalPrice,
                BookingDate = DateTime.UtcNow,
                BookingReference = bookingReference
            };

            // Start transaction to ensure atomicity
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Reduce available rooms
                package.AvailableRooms -= numberOfRooms;

                // Remove from waiting list if applicable
                var waitingEntry = await _context.WaitingListEntries
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.PackageId == packageId);
                if (waitingEntry != null)
                    _context.WaitingListEntries.Remove(waitingEntry);

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Booking created: {bookingReference} for user {userId}, package {packageId}");

                return new BookingResult
                {
                    Success = true,
                    Message = "Booking created successfully",
                    Booking = booking
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error creating booking: {ex.Message}");
                return new BookingResult { Success = false, Message = "An error occurred while creating your booking" };
            }
        }

        public async Task<BookingResult> ConfirmBookingAsync(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.TravelPackage)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return new BookingResult { Success = false, Message = "Booking not found" };

            if (booking.Status != BookingStatus.Pending)
                return new BookingResult { Success = false, Message = "Booking is not in pending status" };

            booking.Status = BookingStatus.Confirmed;
            booking.ConfirmedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send confirmation email
            await _emailService.SendBookingConfirmationAsync(
                booking.User.Email,
                booking.BookingReference,
                booking.TravelPackage.Destination,
                booking.TravelPackage.StartDate);

            _logger.LogInformation($"Booking confirmed: {booking.BookingReference}");

            return new BookingResult
            {
                Success = true,
                Message = "Booking confirmed successfully",
                Booking = booking
            };
        }

        public async Task<BookingResult> CancelBookingAsync(int bookingId, string reason)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.TravelPackage)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return new BookingResult { Success = false, Message = "Booking not found" };

            if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
                return new BookingResult { Success = false, Message = "Booking cannot be cancelled" };

            // Check cancellation policy (e.g., cannot cancel within 3 days of departure)
            var daysUntilTrip = (booking.TravelPackage.StartDate - DateTime.UtcNow).TotalDays;
            if (daysUntilTrip < 3 && daysUntilTrip > 0)
                return new BookingResult { Success = false, Message = "Cannot cancel booking within 3 days of departure" };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Return rooms to availability
                booking.TravelPackage.AvailableRooms += booking.NumberOfRooms;

                booking.Status = BookingStatus.Cancelled;
                booking.CancelledDate = DateTime.UtcNow;
                booking.CancellationReason = reason;

                await _context.SaveChangesAsync();

                // Process waiting list
                await ProcessWaitingListAsync(booking.PackageId);

                await transaction.CommitAsync();

                // Send cancellation email
                await _emailService.SendBookingCancellationAsync(
                    booking.User.Email,
                    booking.BookingReference,
                    booking.TravelPackage.Destination);

                _logger.LogInformation($"Booking cancelled: {booking.BookingReference}");

                return new BookingResult
                {
                    Success = true,
                    Message = "Booking cancelled successfully",
                    Booking = booking
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error cancelling booking: {ex.Message}");
                return new BookingResult { Success = false, Message = "An error occurred while cancelling your booking" };
            }
        }

        public async Task<List<Booking>> GetUserBookingsAsync(int userId)
        {
            return await _context.Bookings
                .Include(b => b.TravelPackage)
                .Include(b => b.Payment)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();
        }

        public async Task<Booking> GetBookingByIdAsync(int bookingId)
        {
            return await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.TravelPackage)
                    .ThenInclude(p => p.Images)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        }

        public async Task<List<Booking>> GetAllBookingsAsync()
        {
            return await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.TravelPackage)
                .Include(b => b.Payment)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();
        }

        public async Task<int> GetActiveBookingCountAsync(int userId)
        {
            return await _context.Bookings
                .CountAsync(b => b.UserId == userId &&
                               (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
        }

        public async Task<bool> CanUserBookAsync(int userId)
        {
            var activeBookings = await GetActiveBookingCountAsync(userId);
            return activeBookings < MAX_ACTIVE_BOOKINGS;
        }

        // Waiting List Methods
        public async Task<WaitingListResult> JoinWaitingListAsync(int userId, int packageId, int numberOfRooms)
        {
            // Check if already in waiting list
            var existing = await _context.WaitingListEntries
                .FirstOrDefaultAsync(w => w.UserId == userId && w.PackageId == packageId);

            if (existing != null)
                return new WaitingListResult { Success = false, Message = "You are already on the waiting list for this package" };

            // Get next position
            var maxPosition = await _context.WaitingListEntries
                .Where(w => w.PackageId == packageId)
                .MaxAsync(w => (int?)w.Position) ?? 0;

            var entry = new WaitingListEntry
            {
                UserId = userId,
                PackageId = packageId,
                NumberOfRooms = numberOfRooms,
                Position = maxPosition + 1,
                DateJoined = DateTime.UtcNow
            };

            _context.WaitingListEntries.Add(entry);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {userId} joined waiting list for package {packageId} at position {entry.Position}");

            return new WaitingListResult
            {
                Success = true,
                Message = $"You have been added to the waiting list at position {entry.Position}",
                Entry = entry
            };
        }

        public async Task<bool> LeaveWaitingListAsync(int userId, int packageId)
        {
            var entry = await _context.WaitingListEntries
                .FirstOrDefaultAsync(w => w.UserId == userId && w.PackageId == packageId);

            if (entry == null)
                return false;

            var position = entry.Position;
            _context.WaitingListEntries.Remove(entry);

            // Update positions of others
            var laterEntries = await _context.WaitingListEntries
                .Where(w => w.PackageId == packageId && w.Position > position)
                .ToListAsync();

            foreach (var e in laterEntries)
                e.Position--;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<WaitingListEntry>> GetWaitingListAsync(int packageId)
        {
            return await _context.WaitingListEntries
                .Include(w => w.User)
                .Where(w => w.PackageId == packageId)
                .OrderBy(w => w.Position)
                .ToListAsync();
        }

        public async Task<List<WaitingListEntry>> GetUserWaitingListAsync(int userId)
        {
            return await _context.WaitingListEntries
                .Include(w => w.TravelPackage)
                .Where(w => w.UserId == userId)
                .OrderBy(w => w.DateJoined)
                .ToListAsync();
        }

        public async Task<bool> IsUserNextInWaitingListAsync(int userId, int packageId)
        {
            var firstInLine = await _context.WaitingListEntries
                .Where(w => w.PackageId == packageId)
                .OrderBy(w => w.Position)
                .FirstOrDefaultAsync();

            return firstInLine != null && firstInLine.UserId == userId;
        }

        public async Task ProcessWaitingListAsync(int packageId)
        {
            var package = await _context.TravelPackages.FindAsync(packageId);
            if (package == null || package.AvailableRooms <= 0)
                return;

            var nextInLine = await _context.WaitingListEntries
                .Include(w => w.User)
                .Where(w => w.PackageId == packageId && !w.IsNotified)
                .OrderBy(w => w.Position)
                .FirstOrDefaultAsync();

            if (nextInLine != null)
            {
                // Notify user
                await _emailService.SendWaitingListNotificationAsync(
                    nextInLine.User.Email,
                    package.Destination,
                    package.AvailableRooms);

                nextInLine.IsNotified = true;
                nextInLine.DateNotified = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Notified user {nextInLine.UserId} about availability for package {packageId}");
            }
        }

        public async Task<List<Booking>> GetBookingsByPackageAsync(int packageId)
        {
            return await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Payment)
                .Where(b => b.PackageId == packageId)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();
        }

        public async Task SendTripRemindersAsync()
        {
            var reminderDays = new[] { 7, 5, 3, 1 };
            var now = DateTime.UtcNow;

            foreach (var days in reminderDays)
            {
                var targetDate = now.AddDays(days).Date;

                var bookings = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.TravelPackage)
                    .Where(b => b.Status == BookingStatus.Confirmed &&
                               b.TravelPackage.StartDate.Date == targetDate)
                    .ToListAsync();

                foreach (var booking in bookings)
                {
                    await _emailService.SendTripReminderAsync(
                        booking.User.Email,
                        booking.TravelPackage.Destination,
                        booking.TravelPackage.StartDate,
                        days);

                    _logger.LogInformation($"Sent {days}-day reminder to user {booking.UserId} for booking {booking.BookingReference}");
                }
            }
        }

        private string GenerateBookingReference()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"TRV-{timestamp}-{random}";
        }
    }

    public class BookingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Booking Booking { get; set; }
    }

    public class WaitingListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public WaitingListEntry Entry { get; set; }
    }
}