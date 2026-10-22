using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Staybnb.Models;

public class Booking
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty Property { get; set; }
    [Required] public string GuestId { get; set; }
    public ApplicationUser Guest { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; } = 1;
    [Column(TypeName = "decimal(18,2)")] public decimal CleaningFee { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ServiceFee { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Payment Payment { get; set; }
    public GuestCheckIn GuestCheckIn { get; set; }
    [NotMapped] public int Nights => (CheckOutDate.Date - CheckInDate.Date).Days;
}

public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GuestCheckIn
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; }
    public int CheckInProcessId { get; set; }
    public CheckInProcess CheckInProcess { get; set; }
    public CheckInStatus Status { get; set; } = CheckInStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<GuestDocument> Documents { get; set; } = new List<GuestDocument>();
}

public class GuestDocument
{
    public int Id { get; set; }
    public int GuestCheckInId { get; set; }
    public GuestCheckIn GuestCheckIn { get; set; }
    [Required, StringLength(100)] public string DocumentType { get; set; }
    [Required, StringLength(200)] public string FileName { get; set; }
    [StringLength(200)] public string OriginalName { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
}

public class Message
{
    public int Id { get; set; }
    [Required] public string SenderId { get; set; }
    public ApplicationUser Sender { get; set; }
    [Required] public string ReceiverId { get; set; }
    public ApplicationUser Receiver { get; set; }
    [Required] public string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public class Notification
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    [Required, StringLength(100)] public string Title { get; set; }
    public string Message { get; set; }
    public NotificationType Type { get; set; }
    public string Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ActivityLog
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    [Required, StringLength(100)] public string Action { get; set; }
    [StringLength(500)] public string Details { get; set; }
    public ActivityType ActivityType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
