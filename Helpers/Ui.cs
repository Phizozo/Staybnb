using System.Globalization;

namespace Staybnb.Helpers;

public static class Ui
{
    public static string Money(decimal v) => "R " + v.ToString("N2", CultureInfo.InvariantCulture);

    public static string Badge(object status) => status?.ToString() switch
    {
        "Pending" or "Submitted" or "RulesAccepted" => "bg-warning text-dark",
        "Approved" or "Verified" or "Paid" or "Active" => "bg-success",
        "Rejected" or "Cancelled" or "Refunded" or "Inactive" => "bg-danger",
        "CheckedIn" => "bg-primary",
        "Completed" => "bg-secondary",
        _ => "bg-light text-dark"
    };

    public static string Stars(double rating) => rating <= 0 ? "New" : rating.ToString("0.0", CultureInfo.InvariantCulture);
}
