using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRAVEL.Models
{
    /// <summary>
    /// Shopping cart for users to store travel packages before checkout
    /// </summary>
    public class Cart
    {
        [Key]
        public int CartId { get; set; }

        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Date the cart was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date the cart was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this cart is active (not checked out)
        /// </summary>
        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();

        // Computed properties
        [NotMapped]
        public decimal TotalPrice => CalculateTotalPrice();

        [NotMapped]
        public int TotalItems => Items?.Count ?? 0;

        private decimal CalculateTotalPrice()
        {
            decimal total = 0;
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    total += item.Subtotal;
                }
            }
            return total;
        }
    }
}