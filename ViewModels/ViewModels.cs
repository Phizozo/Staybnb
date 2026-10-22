using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

namespace Staybnb.ViewModels;

public class RegisterVM
{
    [Required, StringLength(50), Display(Name = "First name")] public string FirstName { get; set; }
    [Required, StringLength(50), Display(Name = "Last name")] public string LastName { get; set; }
    [Required, EmailAddress] public string Email { get; set; }
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 6)] public string Password { get; set; }
    [DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")] public string ConfirmPassword { get; set; }
}

public class LoginVM
{
    [Required, EmailAddress] public string Email { get; set; }
    [Required, DataType(DataType.Password)] public string Password { get; set; }
    [Display(Name = "Remember me")] public bool RememberMe { get; set; }
    public string ReturnUrl { get; set; }
}

public class PropertyFormVM
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Title { get; set; }
    [Required] public string Description { get; set; }
    [Required, Range(1, 1000000), Display(Name = "Price per night (R)")] public decimal PricePerNight { get; set; }
    [Range(0, 100000), Display(Name = "Cleaning fee (R)")] public decimal CleaningFee { get; set; }
    [Required] public string Address { get; set; }
    [Required, StringLength(100)] public string City { get; set; }
    [Required, Display(Name = "Property type")] public string PropertyType { get; set; }
    [Range(1, 50), Display(Name = "Max guests")] public int MaxGuests { get; set; } = 2;
    [Range(0, 50)] public int Bedrooms { get; set; } = 1;
    [Range(1, 50)] public int Beds { get; set; } = 1;
    [Range(1, 50)] public int Bathrooms { get; set; } = 1;
    public List<int> AmenityIds { get; set; } = new();
    public List<IFormFile> Images { get; set; } = new();
}

public class PropertyIndexVM
{
    public List<HostProperty> Properties { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public HashSet<int> WishlistIds { get; set; } = new();
    public string City { get; set; }
    public string Type { get; set; }
}

public class PropertyDetailsVM
{
    public HostProperty Property { get; set; }
    public bool IsWishlisted { get; set; }
    public bool CanReview { get; set; }
    public double AverageRating => Property.Reviews.Any() ? Property.Reviews.Average(r => r.Rating) : 0;
    public List<string> Unavailable { get; set; } = new();
}

public class HostDashboardVM
{
    public string Name { get; set; }
    public int TotalProperties { get; set; }
    public int ActiveProperties { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int PendingCheckIns { get; set; }
    public decimal TotalEarnings { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public List<Booking> Recent { get; set; } = new();
}

public class AdminDashboardVM
{
    public int TotalUsers { get; set; }
    public int PendingApps { get; set; }
    public int ActiveProperties { get; set; }
    public decimal Revenue { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ApprovedHosts { get; set; }
    public List<ActivityLog> Activity { get; set; } = new();
    public List<HostApplication> Applications { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
}

public class CheckInProcessVM
{
    public int PropertyId { get; set; }
    public string PropertyTitle { get; set; }
    [Required, StringLength(200)] public string Title { get; set; }
    [Display(Name = "House rules (one per line)")] public string Rules { get; set; }
    [Required, Display(Name = "Required documents (one per line)")] public string Documents { get; set; }
}

public class GuestCheckInVM
{
    public Booking Booking { get; set; }
    public GuestCheckIn CheckIn { get; set; }
    public CheckInConfig Config { get; set; }
    public string ProcessTitle { get; set; }
}

public class ConversationVM
{
    public ApplicationUser Partner { get; set; }
    public Message Last { get; set; }
    public int Unread { get; set; }
}

public class ThreadVM
{
    public ApplicationUser Partner { get; set; }
    public List<Message> Messages { get; set; } = new();
}

public class UserRowVM
{
    public ApplicationUser User { get; set; }
    public IList<string> Roles { get; set; }
}

public class NavCountsVM { public int Notifications { get; set; } public int Messages { get; set; } }
