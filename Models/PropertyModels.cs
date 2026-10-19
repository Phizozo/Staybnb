using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Staybnb.Models;

public class HostProperty
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Title { get; set; }
    [Required] public string Description { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PricePerNight { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CleaningFee { get; set; }
    [Required] public string HostId { get; set; }
    public ApplicationUser Host { get; set; }
    [Required] public string Address { get; set; }
    [Required, StringLength(100)] public string City { get; set; }
    [Required, StringLength(50)] public string PropertyType { get; set; }
    public int MaxGuests { get; set; } = 2;
    public int Bedrooms { get; set; } = 1;
    public int Beds { get; set; } = 1;
    public int Bathrooms { get; set; } = 1;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    public CheckInProcess CheckInProcess { get; set; }
}

public class PropertyImage
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; }
    [Required] public string ImageUrl { get; set; }
}

public class Amenity
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string Name { get; set; }
    [StringLength(50)] public string IconClass { get; set; }
    public ICollection<HostProperty> Properties { get; set; } = new List<HostProperty>();
}

public class HostApplication
{
    public int Id { get; set; }
    [Required] public string ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    [StringLength(500)] public string AdminNote { get; set; }
}

public class Review
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; }
    [Required] public string ReviewerId { get; set; }
    public ApplicationUser Reviewer { get; set; }
    [Range(1, 5)] public int Rating { get; set; }
    [StringLength(1000)] public string Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WishlistItem
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CheckInProcess
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; }
    [Required, StringLength(200)] public string Title { get; set; }
    /// <summary>JSON serialised <see cref="CheckInConfig"/> (rules + required documents).</summary>
    public string StepsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped] public CheckInConfig Config => CheckInConfig.Parse(StepsJson);
}

public class CheckInConfig
{
    public List<string> Rules { get; set; } = new();
    public List<string> Documents { get; set; } = new();

    public static CheckInConfig Default() => new()
    {
        Rules = new List<string> { "Check-in is from 14:00 and check-out by 10:00.", "No parties or events.", "Respect the neighbours and quiet hours." },
        Documents = new List<string> { "ID or Passport" }
    };

    public static CheckInConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Default();
        try
        {
            var c = JsonSerializer.Deserialize<CheckInConfig>(json);
            if (c == null) return Default();
            if (c.Documents == null || c.Documents.Count == 0) c.Documents = new List<string> { "ID or Passport" };
            c.Rules ??= new List<string>();
            return c;
        }
        catch { return Default(); }
    }

    public string ToJson() => JsonSerializer.Serialize(this);
}
