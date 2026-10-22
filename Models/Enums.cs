namespace Staybnb.Models;

public static class Roles
{
    public const string Guest = "Guest", Host = "Host", Admin = "Admin", SuperAdmin = "SuperAdmin";
    public static readonly string[] All = { Guest, Host, Admin, SuperAdmin };
}

public static class PropertyTypes
{
    public static readonly string[] All = { "Apartment", "House", "Villa", "Cabin", "Cottage", "Beachfront", "Guesthouse" };
}

public enum BookingStatus { Pending, Approved, Rejected, CheckedIn, Completed, Cancelled }
public enum PaymentStatus { Pending, Paid, Refunded }
public enum ApplicationStatus { Pending, Approved, Rejected }
public enum ActivityType { Login, Logout, Registration, RoleChange, BookingUpdate, Application, PropertyUpdate, CheckIn, Message, System }
public enum NotificationType { Info, Success, Warning, Booking, Message, Application }
public enum CheckInStatus { Pending, RulesAccepted, Submitted, Verified, Rejected }
public enum DocumentStatus { Pending, Verified, Rejected }
