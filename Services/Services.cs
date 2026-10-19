using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Services;

public record BookingQuote(int Nights, decimal Subtotal, decimal CleaningFee, decimal ServiceFee, decimal Total);

public static class BookingCalculator
{
    public const decimal ServiceFeeRate = 0.12m;

    /// <summary>TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee</summary>
    public static BookingQuote Calculate(decimal pricePerNight, decimal cleaningFee, int nights)
    {
        var subtotal = pricePerNight * nights;
        var service = Math.Round(subtotal * ServiceFeeRate, 2);
        return new BookingQuote(nights, subtotal, cleaningFee, service, subtotal + cleaningFee + service);
    }
}

public class ActivityLogger
{
    private readonly ApplicationDbContext _db;
    public ActivityLogger(ApplicationDbContext db) => _db = db;

    public async Task LogAsync(string userId, string action, ActivityType type, string details = null)
    {
        _db.ActivityLogs.Add(new ActivityLog { UserId = userId, Action = action, ActivityType = type, Details = details });
        await _db.SaveChangesAsync();
    }
}

public class NotificationService
{
    private readonly ApplicationDbContext _db;
    public NotificationService(ApplicationDbContext db) => _db = db;

    public async Task SendAsync(string userId, string title, string message, NotificationType type, string link = null)
    {
        _db.Notifications.Add(new Notification { UserId = userId, Title = title, Message = message, Type = type, Link = link });
        await _db.SaveChangesAsync();
    }
}

public class FileStorage
{
    private static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private static readonly string[] DocExt = { ".jpg", ".jpeg", ".png", ".pdf" };
    private readonly IWebHostEnvironment _env;
    public FileStorage(IWebHostEnvironment env) => _env = env;

    public static bool IsValidImage(IFormFile f) =>
        f != null && f.Length > 0 && f.Length <= 5_000_000 && ImageExt.Contains(Path.GetExtension(f.FileName).ToLowerInvariant());
    public static bool IsValidDocument(IFormFile f) =>
        f != null && f.Length > 0 && f.Length <= 5_000_000 && DocExt.Contains(Path.GetExtension(f.FileName).ToLowerInvariant());

    public async Task<string> SaveImageAsync(IFormFile file)
    {
        var dir = Path.Combine(_env.WebRootPath, "uploads", "properties");
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var fs = new FileStream(Path.Combine(dir, name), FileMode.Create);
        await file.CopyToAsync(fs);
        return "/uploads/properties/" + name;
    }

    // Identity documents are stored OUTSIDE wwwroot and only served through an authorised controller action.
    public async Task<string> SaveDocumentAsync(IFormFile file)
    {
        var dir = Path.Combine(_env.ContentRootPath, "App_Data", "documents");
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var fs = new FileStream(Path.Combine(dir, name), FileMode.Create);
        await file.CopyToAsync(fs);
        return name;
    }

    public string DocumentPath(string fileName) =>
        Path.Combine(_env.ContentRootPath, "App_Data", "documents", Path.GetFileName(fileName));
}

public class PropertyApplicationService
{
    private readonly ApplicationDbContext _db;
    private readonly FileStorage _files;
    private readonly UserManager<ApplicationUser> _um;
    private readonly ActivityLogger _log;
    private readonly NotificationService _notify;

    public PropertyApplicationService(ApplicationDbContext db, FileStorage files, UserManager<ApplicationUser> um, ActivityLogger log, NotificationService notify)
    { _db = db; _files = files; _um = um; _log = log; _notify = notify; }

    /// <summary>Returns an error message, or null when valid. A property cannot be submitted without at least one valid image.</summary>
    public string ValidateImages(PropertyFormVM vm)
    {
        var imgs = vm.Images?.Where(i => i != null && i.Length > 0).ToList() ?? new();
        if (imgs.Count == 0) return "At least one property image is required.";
        if (imgs.Any(i => !FileStorage.IsValidImage(i))) return "Images must be JPG, PNG, WEBP or GIF and under 5MB each.";
        return null;
    }

    public async Task<HostApplication> SubmitAsync(string userId, PropertyFormVM vm, bool additional)
    {
        var property = new HostProperty
        {
            Title = vm.Title, Description = vm.Description, PricePerNight = vm.PricePerNight, CleaningFee = vm.CleaningFee,
            Address = vm.Address, City = vm.City, PropertyType = vm.PropertyType, MaxGuests = vm.MaxGuests,
            Bedrooms = vm.Bedrooms, Beds = vm.Beds, Bathrooms = vm.Bathrooms, HostId = userId, IsActive = false
        };
        foreach (var img in vm.Images.Where(i => i != null && i.Length > 0))
            property.Images.Add(new PropertyImage { ImageUrl = await _files.SaveImageAsync(img) });
        if (vm.AmenityIds?.Any() == true)
            property.Amenities = await _db.Amenities.Where(a => vm.AmenityIds.Contains(a.Id)).ToListAsync();

        var application = new HostApplication { ApplicationUserId = userId, Property = property, Status = ApplicationStatus.Pending };
        _db.HostApplications.Add(application);
        await _db.SaveChangesAsync();

        var action = additional ? "AdditionalPropertyApplicationSubmitted" : "HostApplicationSubmitted";
        await _log.LogAsync(userId, action, ActivityType.Application, property.Title);
        await _notify.SendAsync(userId, additional ? "Additional Property Application Submitted" : "Host Application Submitted",
            $"Your application for '{property.Title}' has been received and is under review.", NotificationType.Application,
            "/Guest/Applications");

        var admins = (await _um.GetUsersInRoleAsync(Roles.Admin)).Concat(await _um.GetUsersInRoleAsync(Roles.SuperAdmin)).DistinctBy(u => u.Id);
        foreach (var a in admins)
            await _notify.SendAsync(a.Id, "New host application", $"'{property.Title}' is waiting for review.", NotificationType.Application, $"/Admin/Application/{application.Id}");
        return application;
    }
}
