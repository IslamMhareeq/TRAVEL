using Microsoft.EntityFrameworkCore;
using TRAVEL.Models;

namespace TRAVEL.Data
{
    /// <summary>
    /// Entity Framework Core DbContext for Travel Agency application
    /// </summary>
    public class TravelDbContext : DbContext
    {
        /// <summary>
        /// Constructor for TravelDbContext
        /// </summary>
        public TravelDbContext(DbContextOptions<TravelDbContext> options) : base(options)
        {
        }

        // ===== DbSets =====
        public DbSet<WishlistItem> Wishlists { get; set; }
        /// <summary>
        /// Users table
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// Travel packages table
        /// </summary>
        public DbSet<TravelPackage> TravelPackages { get; set; }

        /// <summary>
        /// Package images table
        /// </summary>
        public DbSet<PackageImage> PackageImages { get; set; }

        /// <summary>
        /// Bookings table
        /// </summary>
        public DbSet<Booking> Bookings { get; set; }

        /// <summary>
        /// Payments table
        /// </summary>
        public DbSet<Payment> Payments { get; set; }

        /// <summary>
        /// Reviews table
        /// </summary>
        public DbSet<Review> Reviews { get; set; }

        /// <summary>
        /// Waiting list entries table
        /// </summary>
        public DbSet<WaitingListEntry> WaitingListEntries { get; set; }

        // ===== MODEL CONFIGURATION =====

        /// <summary>
        /// Configures entity relationships and constraints
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== USER CONFIGURATION =====
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);

                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.HasIndex(e => e.Email)
                    .IsUnique();

                entity.Property(e => e.FirstName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.LastName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.PasswordHash)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Role)
                    .HasConversion<int>();

                entity.Property(e => e.Status)
                    .HasConversion<int>();

                entity.ToTable("Users");
            });
            modelBuilder.Entity<WishlistItem>(entity =>
            {
                entity.HasKey(e => e.WishlistId);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Wishlist)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.TravelPackage)
                    .WithMany()
                    .HasForeignKey(e => e.PackageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.DateAdded)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(e => new { e.UserId, e.PackageId }).IsUnique();

                entity.ToTable("Wishlists");
            });
            // ===== TRAVEL PACKAGE CONFIGURATION =====
            modelBuilder.Entity<TravelPackage>(entity =>
            {
                entity.HasKey(e => e.PackageId);

                entity.Property(e => e.Destination)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Country)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Price)
                    .HasPrecision(10, 2);

                entity.Property(e => e.DiscountedPrice)
                    .HasPrecision(10, 2);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.PackageType)
                    .HasConversion<int>();

                entity.ToTable("TravelPackages");
            });

            // ===== PACKAGE IMAGE CONFIGURATION =====
            modelBuilder.Entity<PackageImage>(entity =>
            {
                entity.HasKey(e => e.ImageId);

                entity.HasOne(e => e.TravelPackage)
                    .WithMany(p => p.Images)
                    .HasForeignKey(e => e.PackageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable("PackageImages");
            });

            // ===== BOOKING CONFIGURATION =====
            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasKey(e => e.BookingId);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Bookings)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.TravelPackage)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(e => e.PackageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.BookingDate)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Status)
                    .HasConversion<int>();

                entity.Property(e => e.TotalPrice)
                    .HasPrecision(10, 2);

                entity.ToTable("Bookings");
            });

            // ===== PAYMENT CONFIGURATION =====
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.PaymentId);

                entity.HasOne(e => e.Booking)
                    .WithOne(b => b.Payment)
                    .HasForeignKey<Payment>(e => e.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Amount)
                    .HasPrecision(10, 2);

                entity.Property(e => e.PaymentDate)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Status)
                    .HasConversion<int>();

                entity.Property(e => e.PaymentMethod)
                    .HasConversion<int>();

                entity.ToTable("Payments");
            });

            // ===== REVIEW CONFIGURATION =====
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasKey(e => e.ReviewId);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Reviews)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.TravelPackage)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(e => e.PackageId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.ToTable("Reviews");
            });

            // ===== WAITING LIST ENTRY CONFIGURATION =====
            modelBuilder.Entity<WaitingListEntry>(entity =>
            {
                entity.HasKey(e => e.WaitingListId);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.WaitingListEntries)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.TravelPackage)
                    .WithMany(p => p.WaitingList)
                    .HasForeignKey(e => e.PackageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.DateAdded)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.ToTable("WaitingListEntries");
            });

            // ===== INDEXES =====

            // User indexes
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Booking indexes
            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.UserId);

            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.PackageId);

            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.BookingReference)
                .IsUnique();

            // Review indexes
            modelBuilder.Entity<Review>()
                .HasIndex(r => r.UserId);

            modelBuilder.Entity<Review>()
                .HasIndex(r => r.PackageId);

            // Waiting list indexes
            modelBuilder.Entity<WaitingListEntry>()
                .HasIndex(w => w.UserId);

            modelBuilder.Entity<WaitingListEntry>()
                .HasIndex(w => w.PackageId);

            // Payment indexes
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.BookingId)
                .IsUnique();

            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.TransactionId);

            // Travel package indexes
            modelBuilder.Entity<TravelPackage>()
                .HasIndex(p => p.Destination);

            modelBuilder.Entity<TravelPackage>()
                .HasIndex(p => p.Country);
        }
    }
}