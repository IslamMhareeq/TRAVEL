using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRAVEL.Models
{
    /// <summary>
    /// Shopping cart for a user
    /// </summary>
    [Table("Carts")]
    public class CartModels
    {
        [Key]
        public int CartId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();

        // Computed properties
        [NotMapped]
        public decimal Total => Items != null ? Items.Sum(i => i.Subtotal) : 0;

        [NotMapped]
        public int ItemCount => Items?.Count ?? 0;
    }

    /// <summary>
    /// Shopping cart item
    /// </summary>
    [Table("CartItems")]
    public class CartItem
    {
        [Key]
        public int CartItemId { get; set; }

        [Required]
        [ForeignKey("Cart")]
        public int CartId { get; set; }

        [Required]
        [ForeignKey("TravelPackage")]
        public int PackageId { get; set; }

        [Required]
        [Range(1, 10)]
        public int Quantity { get; set; } = 1;

        [Required]
        public int NumberOfGuests { get; set; } = 1;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [StringLength(500)]
        public string? SpecialRequests { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual CartModels? Cart { get; set; }
        public virtual TravelPackage? TravelPackage { get; set; }

        // Computed property
        [NotMapped]
        public decimal Subtotal => Quantity * UnitPrice;
    }
}