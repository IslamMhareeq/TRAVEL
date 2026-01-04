using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRAVEL.Models
{
    /// <summary>
    /// Individual item in a shopping cart
    /// </summary>
    public class CartItem
    {
        [Key]
        public int CartItemId { get; set; }

        [Required]
        public int CartId { get; set; }

        [Required]
        public int PackageId { get; set; }

        /// <summary>
        /// Number of rooms/units being booked
        /// </summary>
        [Required]
        [Range(1, 10)]
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Number of guests for this booking
        /// </summary>
        [Required]
        [Range(1, 20)]
        public int NumberOfGuests { get; set; } = 1;

        /// <summary>
        /// Price at the time item was added (captures discounts)
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Date the item was added to cart
        /// </summary>
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Special requests or notes for this booking
        /// </summary>
        [MaxLength(500)]
        public string? SpecialRequests { get; set; }

        // Navigation properties
        [ForeignKey("CartId")]
        public virtual Cart? Cart { get; set; }

        [ForeignKey("PackageId")]
        public virtual TravelPackage? TravelPackage { get; set; }

        // Computed properties
        [NotMapped]
        public decimal Subtotal => UnitPrice * Quantity;
    }
}