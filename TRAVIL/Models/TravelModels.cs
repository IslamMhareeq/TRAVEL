using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace TRAVEL.Models
{
    // ===== ENUMS =====

    public enum PackageType
    {
        Family = 0,
        Honeymoon = 1,
        Adventure = 2,
        Cruise = 3,
        Luxury = 4,
        Budget = 5,
        Cultural = 6,
        Beach = 7,
        Mountain = 8
    }

    public enum BookingStatus
    {
        Pending = 0,
        Confirmed = 1,
        Cancelled = 2,
        Completed = 3
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Completed = 1,
        Failed = 2,
        Refunded = 3
    }

    public enum PaymentMethod
    {
        CreditCard = 0,
        DebitCard = 1,
        PayPal = 2,
        BankTransfer = 3
    }

    // ===== TRAVEL PACKAGE =====

    [Table("TravelPackages")]
    public class TravelPackage
    {
        [Key]
        public int PackageId { get; set; }

        [Required]
        [StringLength(100)]
        public string Destination { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Country { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DiscountedPrice { get; set; }

        public DateTime? DiscountStartDate { get; set; }

        public DateTime? DiscountEndDate { get; set; }

        [Required]
        public int AvailableRooms { get; set; }

        [Required]
        public PackageType PackageType { get; set; }

        public int? MinimumAge { get; set; }

        public int? MaximumAge { get; set; }

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Itinerary { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        // Navigation properties
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
        public virtual ICollection<WaitingListEntry> WaitingList { get; set; } = new List<WaitingListEntry>();
        public virtual ICollection<PackageImage> Images { get; set; } = new List<PackageImage>();
    }

    // ===== PACKAGE IMAGE =====
    [Table("Wishlists")]
    public class WishlistItem
    {
        [Key]
        public int WishlistId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("TravelPackage")]
        public int PackageId { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual TravelPackage TravelPackage { get; set; } = null!;
    }

    [Table("PackageImages")]
    public class PackageImage
    {
        [Key]
        public int ImageId { get; set; }

        [Required]
        [ForeignKey("TravelPackage")]
        public int PackageId { get; set; }

        [Required]
        [StringLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        [StringLength(200)]
        public string? AltText { get; set; }

        public int DisplayOrder { get; set; }

        public virtual TravelPackage TravelPackage { get; set; } = null!;
    }

    // ===== BOOKING =====

    [Table("Bookings")]
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("TravelPackage")]
        public int PackageId { get; set; }

        [Required]
        public int NumberOfRooms { get; set; }

        [Required]
        public int NumberOfGuests { get; set; }

        [Required]
        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalPrice { get; set; }

        public DateTime BookingDate { get; set; } = DateTime.UtcNow;

        public DateTime? ConfirmedDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        [StringLength(500)]
        public string? CancellationReason { get; set; }

        [StringLength(100)]
        public string? BookingReference { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual TravelPackage TravelPackage { get; set; } = null!;
        public virtual Payment? Payment { get; set; }
    }

    // ===== PAYMENT =====

    [Table("Payments")]
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        [ForeignKey("Booking")]
        public int BookingId { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        [StringLength(255)]
        public string? TransactionId { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedDate { get; set; }

        [StringLength(500)]
        public string? FailureReason { get; set; }

        // Navigation property
        public virtual Booking Booking { get; set; } = null!;
    }

    // ===== REVIEW =====

    [Table("Reviews")]
    public class Review
    {
        [Key]
        public int ReviewId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("TravelPackage")]
        public int? PackageId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        [StringLength(1000)]
        public string Comment { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ReviewType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsApproved { get; set; } = false;

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual TravelPackage? TravelPackage { get; set; }
    }

    // ===== WAITING LIST =====

    [Table("WaitingListEntries")]
    public class WaitingListEntry
    {
        [Key]
        public int WaitingListId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("TravelPackage")]
        public int PackageId { get; set; }

        [Required]
        public int NumberOfRooms { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        public DateTime? DateNotified { get; set; }

        public bool IsNotified { get; set; } = false;

        [StringLength(100)]
        public string? Position { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual TravelPackage TravelPackage { get; set; } = null!;
    }
}