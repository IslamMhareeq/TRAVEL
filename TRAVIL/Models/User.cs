using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TRAVEL.Models
{
    // ===== ENUMS =====

    /// <summary>
    /// User roles in the system
    /// </summary>
    public enum UserRole
    {
        Admin = 0,
        User = 1
    }

    /// <summary>
    /// User account status
    /// </summary>
    public enum UserStatus
    {
        Active = 0,
        Inactive = 1,
        Suspended = 2,
        Deleted = 3
    }

    // ===== USER ENTITY =====

    /// <summary>
    /// Represents a user in the system
    /// </summary>
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 100 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 100 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [StringLength(255)]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [Display(Name = "Password Hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [Display(Name = "User Role")]
        public UserRole Role { get; set; } = UserRole.User;

        [Required]
        [Display(Name = "Account Status")]
        public UserStatus Status { get; set; } = UserStatus.Active;

        [StringLength(20)]
        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Address")]
        public string Address { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "City")]
        public string City { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Country")]
        public string Country { get; set; } = string.Empty;

        [StringLength(10)]
        [Display(Name = "Postal Code")]
        public string PostalCode { get; set; } = string.Empty;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Last Updated")]
        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "Last Login")]
        public DateTime? LastLoginAt { get; set; }

        [StringLength(500)]
        [Display(Name = "Profile Image URL")]
        public string? ProfileImageUrl { get; set; }

        [Display(Name = "Email Verified")]
        public bool EmailVerified { get; set; } = false;

        [Display(Name = "Email Verified Date")]
        public DateTime? EmailVerifiedAt { get; set; }

        // ===== NAVIGATION PROPERTIES =====
        public virtual ICollection<WishlistItem> Wishlist { get; set; } = new List<WishlistItem>();
        [Display(Name = "Bookings")]
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        [Display(Name = "Reviews")]
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

        [Display(Name = "Waiting List Entries")]
        public virtual ICollection<WaitingListEntry> WaitingListEntries { get; set; } = new List<WaitingListEntry>();
    }

    // ===== LOGIN/REGISTER REQUEST DTOs =====

    /// <summary>
    /// Login request model
    /// </summary>
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; } = false;
    }

    /// <summary>
    /// User registration request model
    /// </summary>
    public class RegisterRequest
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 100 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 100 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone format")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [StringLength(100)]
        [Display(Name = "City")]
        public string? City { get; set; }

        [StringLength(100)]
        [Display(Name = "Country")]
        public string? Country { get; set; }

        [StringLength(10)]
        [Display(Name = "Postal Code")]
        public string? PostalCode { get; set; }
    }

    // ===== LOGIN/REGISTER RESPONSE DTOs =====

    /// <summary>
    /// Login response model
    /// </summary>
    public class LoginResponse
    {
        [Display(Name = "Success")]
        public bool Success { get; set; }

        [Display(Name = "Message")]
        public string Message { get; set; } = string.Empty;

        [Display(Name = "JWT Token")]
        public string Token { get; set; } = string.Empty;

        [Display(Name = "User")]
        public UserDto? User { get; set; }
    }

    /// <summary>
    /// User data transfer object for API responses
    /// </summary>
    public class UserDto
    {
        [Display(Name = "User ID")]
        public int UserId { get; set; }

        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Role")]
        public UserRole Role { get; set; }

        [Display(Name = "Status")]
        public UserStatus Status { get; set; }

        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Last Login")]
        public DateTime? LastLoginAt { get; set; }

        [Display(Name = "Email Verified")]
        public bool EmailVerified { get; set; }
    }
}