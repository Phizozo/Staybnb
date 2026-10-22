using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Host-only area: Guests cannot reach these pages.
[Authorize(Roles = "Host")]
public class HostController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PropertyApplicationService _apps;
    private readonly FileStorage _files;
    private readonly ActivityLogger _log;
    private readonly NotificationService _notify;
    private readonly CheckInService _checkIn;

    public HostController(ApplicationDbContext db, PropertyApplicationService apps, FileStorage files, ActivityLogger log, NotificationService notify, CheckInService checkIn)
    { _db = db; _apps = apps; _files = files; _log = log; _notify = notify; _checkIn = checkIn; }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // Properties whose application has been approved (pending/rejected ones are shown separately)
    private IQueryable<HostProperty> MyProperties() =>
        _db.HostProperties.Where(p => p.HostId == Uid && !_db.HostApplications.Any(a => a.PropertyId == p.Id && a.Status != ApplicationStatus.Approved));

    private IQueryable<Booking> MyBookings() => _db.Bookings.Where(b => b.Property.HostId == Uid);

    public async Task<IActionResult> Dashboard()
    {
        var me = await _db.Users.FindAsync(Uid);
        var props = await MyProperties().ToListAsync();
        var reviews = await _db.Reviews.Where(r => r.Property.HostId == Uid).ToListAsync();
        var vm = new HostDashboardVM
        {
            Name = me.FirstName,
            TotalProperties = props.Count, ActiveProperties = props.Count(p => p.IsActive),
            TotalBookings = await MyBookings().CountAsync(),
            PendingBookings = await MyBookings().CountAsync(b => b.Status == BookingStatus.Pending),
            PendingCheckIns = await _db.GuestCheckIns.CountAsync(c => c.Booking.Property.HostId == Uid && c.Status == CheckInStatus.Submitted),
            TotalEarnings = await _db.Payments.Where(p => p.Booking.Property.HostId == Uid && p.Status == PaymentStatus.Paid).SumAsync(p => (decimal?)p.Amount) ?? 0,
            ReviewCount = reviews.Count, AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0,
            Recent = await MyBookings().Include(b => b.Property).Include(b => b.Guest).OrderByDescending(b => b.CreatedAt).Take(5).ToListAsync()
        };
        return View(vm);
    }

    // ---------- Properties ----------
    public async Task<IActionResult> Properties()
    {
        ViewBag.Applications = await _db.HostApplications.Include(a => a.Property)
            .Where(a => a.ApplicationUserId == Uid && a.Status != ApplicationStatus.Approved).OrderByDescending(a => a.AppliedAt).ToListAsync();
        return View(await MyProperties().Include(p => p.Images).Include(p => p.Bookings).Include(p => p.Reviews).OrderByDescending(p => p.CreatedAt).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        var p = await MyProperties().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        p.IsActive = !p.IsActive;
        await _db.SaveChangesAsync();
        await _log.LogAsync(Uid, p.IsActive ? "PropertyActivated" : "PropertyDeactivated", ActivityType.PropertyUpdate, p.Title);
        return RedirectToAction(nameof(Properties));
    }

    [HttpGet]
    public async Task<IActionResult> AddProperty()
    {
        ViewBag.Amenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
        return View(new PropertyFormVM());
    }

    [HttpPost]
    public async Task<IActionResult> AddProperty(PropertyFormVM vm)
    {
        var imgError = _apps.ValidateImages(vm);
        if (imgError != null) ModelState.AddModelError("Images", imgError);
        if (!ModelState.IsValid)
        {
            ViewBag.Amenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
            return View(vm);
        }
        await _apps.SubmitAsync(Uid, vm, additional: true); // additional properties also need admin approval
        TempData["Success"] = "Property submitted for admin approval.";
        return RedirectToAction(nameof(Properties));
    }

    [HttpGet]
    public async Task<IActionResult> EditProperty(int id)
    {
        var p = await MyProperties().Include(x => x.Amenities).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        ViewBag.Amenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
        return View(new PropertyFormVM
        {
            Id = p.Id, Title = p.Title, Description = p.Description, PricePerNight = p.PricePerNight, CleaningFee = p.CleaningFee,
            Address = p.Address, City = p.City, PropertyType = p.PropertyType, MaxGuests = p.MaxGuests, Bedrooms = p.Bedrooms,
            Beds = p.Beds, Bathrooms = p.Bathrooms, AmenityIds = p.Amenities.Select(a => a.Id).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditProperty(PropertyFormVM vm)
    {
        var p = await MyProperties().Include(x => x.Amenities).FirstOrDefaultAsync(x => x.Id == vm.Id);
        if (p == null) return NotFound();
        var newImages = vm.Images?.Where(i => i != null && i.Length > 0).ToList() ?? new();
        if (newImages.Any(i => !FileStorage.IsValidImage(i))) ModelState.AddModelError("Images", "Images must be JPG, PNG, WEBP or GIF under 5MB.");
        if (!ModelState.IsValid)
        {
            ViewBag.Amenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
            return View(vm);
        }
        p.Title = vm.Title; p.Description = vm.Description; p.PricePerNight = vm.PricePerNight; p.CleaningFee = vm.CleaningFee;
        p.Address = vm.Address; p.City = vm.City; p.PropertyType = vm.PropertyType; p.MaxGuests = vm.MaxGuests;
        p.Bedrooms = vm.Bedrooms; p.Beds = vm.Beds; p.Bathrooms = vm.Bathrooms;
        p.Amenities = await _db.Amenities.Where(a => vm.AmenityIds.Contains(a.Id)).ToListAsync();
        foreach (var img in newImages) _db.PropertyImages.Add(new PropertyImage { PropertyId = p.Id, ImageUrl = await _files.SaveImageAsync(img) });
        await _db.SaveChangesAsync();
        await _log.LogAsync(Uid, "PropertyUpdated", ActivityType.PropertyUpdate, p.Title);
        TempData["Success"] = "Property updated.";
        return RedirectToAction(nameof(Properties));
    }

    // ---------- Bookings ----------
    public async Task<IActionResult> Bookings() =>
        View(await MyBookings().Include(b => b.Property).Include(b => b.Guest).Include(b => b.Payment).Include(b => b.GuestCheckIn)
            .OrderBy(b => b.Status == BookingStatus.Pending ? 0 : 1).ThenByDescending(b => b.CreatedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> BookingAction(int id, string act)
    {
        var b = await MyBookings().Include(x => x.Property).Include(x => x.Payment).FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return NotFound();
        string title = null, msg = null;

        if (act == "approve" && b.Status == BookingStatus.Pending)
        {
            var clash = await _db.Bookings.AnyAsync(o => o.Id != b.Id && o.PropertyId == b.PropertyId
                && (o.Status == BookingStatus.Approved || o.Status == BookingStatus.CheckedIn)
                && o.CheckInDate < b.CheckOutDate && o.CheckOutDate > b.CheckInDate);
            if (clash) { TempData["Error"] = "These dates overlap another approved booking."; return RedirectToAction(nameof(Bookings)); }
            b.Status = BookingStatus.Approved;
            if (b.Payment != null) b.Payment.Status = PaymentStatus.Paid; // payment is simulated
            title = "Booking Approved"; msg = $"Your booking for '{b.Property.Title}' was approved. Complete your check-in.";
        }
        else if (act == "reject" && b.Status == BookingStatus.Pending)
        {
            b.Status = BookingStatus.Rejected;
            if (b.Payment != null) b.Payment.Status = PaymentStatus.Refunded;
            title = "Booking Rejected"; msg = $"Your booking for '{b.Property.Title}' was declined.";
        }
        else if (act == "complete" && b.Status == BookingStatus.CheckedIn && DateTime.Today >= b.CheckOutDate.Date)
        {
            b.Status = BookingStatus.Completed;
            title = "Stay Completed"; msg = $"Your stay at '{b.Property.Title}' is complete. Leave a review!";
        }
        else { TempData["Error"] = "That action isn't available for this booking."; return RedirectToAction(nameof(Bookings)); }

        await _db.SaveChangesAsync();
        await _log.LogAsync(Uid, "BookingStatusChanged", ActivityType.BookingUpdate, $"Booking #{b.Id} -> {b.Status}");
        await _notify.SendAsync(b.GuestId, title, msg, NotificationType.Booking, "/Guest/Bookings");
        TempData["Success"] = $"Booking {b.Status.ToString().ToLower()}.";
        return RedirectToAction(nameof(Bookings));
    }

    // ---------- Check-in configuration & verification ----------
    [HttpGet]
    public async Task<IActionResult> CheckInProcess(int propertyId)
    {
        var p = await MyProperties().FirstOrDefaultAsync(x => x.Id == propertyId);
        if (p == null) return NotFound();
        var proc = await _checkIn.EnsureProcessAsync(p.Id, p.Title);
        return View(new CheckInProcessVM
        {
            PropertyId = p.Id, PropertyTitle = p.Title, Title = proc.Title,
            Rules = string.Join("\n", proc.Config.Rules), Documents = string.Join("\n", proc.Config.Documents)
        });
    }

    [HttpPost]
    public async Task<IActionResult> CheckInProcess(CheckInProcessVM vm)
    {
        var p = await MyProperties().FirstOrDefaultAsync(x => x.Id == vm.PropertyId);
        if (p == null) return NotFound();
        if (!ModelState.IsValid) { vm.PropertyTitle = p.Title; return View(vm); }
        static List<string> Lines(string s) => (s ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var docs = Lines(vm.Documents);
        if (docs.Count == 0) { ModelState.AddModelError("Documents", "Require at least one document."); vm.PropertyTitle = p.Title; return View(vm); }
        var proc = await _checkIn.EnsureProcessAsync(p.Id, p.Title);
        proc.Title = vm.Title;
        proc.StepsJson = new CheckInConfig { Rules = Lines(vm.Rules), Documents = docs }.ToJson();
        await _db.SaveChangesAsync();
        await _log.LogAsync(Uid, "CheckInProcessConfigured", ActivityType.PropertyUpdate, p.Title);
        TempData["Success"] = "Check-in process saved.";
        return RedirectToAction(nameof(Properties));
    }

    public async Task<IActionResult> CheckIns() =>
        View(await _db.GuestCheckIns.Include(c => c.Booking).ThenInclude(b => b.Property).Include(c => c.Booking).ThenInclude(b => b.Guest)
            .Where(c => c.Booking.Property.HostId == Uid && c.Status != CheckInStatus.Pending)
            .OrderBy(c => c.Status == CheckInStatus.Submitted ? 0 : 1).ThenByDescending(c => c.CreatedAt).ToListAsync());

    public async Task<IActionResult> CheckInDetail(int id)
    {
        var ci = await _db.GuestCheckIns.Include(c => c.Documents).Include(c => c.CheckInProcess)
            .Include(c => c.Booking).ThenInclude(b => b.Property).Include(c => c.Booking).ThenInclude(b => b.Guest)
            .FirstOrDefaultAsync(c => c.Id == id && c.Booking.Property.HostId == Uid);
        return ci == null ? NotFound() : View(ci);
    }

    [HttpPost]
    public async Task<IActionResult> DocumentAction(int id, string act)
    {
        var doc = await _db.GuestDocuments.Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
            .FirstOrDefaultAsync(d => d.Id == id && d.GuestCheckIn.Booking.Property.HostId == Uid);
        if (doc == null) return NotFound();
        var ci = doc.GuestCheckIn;
        if (ci.Status != CheckInStatus.Submitted) { TempData["Error"] = "This check-in is not awaiting verification."; return RedirectToAction(nameof(CheckInDetail), new { id = ci.Id }); }

        doc.Status = act == "verify" ? DocumentStatus.Verified : DocumentStatus.Rejected;
        await _db.SaveChangesAsync();

        var docs = await _db.GuestDocuments.Where(d => d.GuestCheckInId == ci.Id).ToListAsync();
        var required = (await _db.CheckInProcesses.FindAsync(ci.CheckInProcessId)).Config.Documents;
        var booking = ci.Booking;
        if (docs.Any(d => d.Status == DocumentStatus.Rejected))
        {
            ci.Status = CheckInStatus.Rejected;
            await _notify.SendAsync(booking.GuestId, "Check-in Document Rejected", $"Please re-upload your document for '{booking.Property.Title}'.", NotificationType.Warning, $"/Guest/CheckIn?bookingId={booking.Id}");
        }
        else if (required.All(r => docs.Any(d => d.DocumentType == r && d.Status == DocumentStatus.Verified)))
        {
            ci.Status = CheckInStatus.Verified;
            booking.Status = BookingStatus.CheckedIn; // Pending -> Approved -> CheckedIn
            await _log.LogAsync(Uid, "GuestCheckedIn", ActivityType.BookingUpdate, $"Booking #{booking.Id} -> CheckedIn");
            await _notify.SendAsync(booking.GuestId, "Check-in Verified", $"You're checked in at '{booking.Property.Title}'. Enjoy your stay!", NotificationType.Success, "/Guest/Bookings");
        }
        await _db.SaveChangesAsync();
        await _log.LogAsync(Uid, "GuestDocument" + doc.Status, ActivityType.CheckIn, $"Document #{doc.Id}");
        return RedirectToAction(nameof(CheckInDetail), new { id = ci.Id });
    }

    // ---------- Earnings & reviews ----------
    public async Task<IActionResult> Earnings() =>
        View(await _db.Payments.Include(p => p.Booking).ThenInclude(b => b.Property).Include(p => p.Booking).ThenInclude(b => b.Guest)
            .Where(p => p.Booking.Property.HostId == Uid && p.Status == PaymentStatus.Paid).OrderByDescending(p => p.CreatedAt).ToListAsync());

    public async Task<IActionResult> Reviews() =>
        View(await _db.Reviews.Include(r => r.Property).Include(r => r.Reviewer).Where(r => r.Property.HostId == Uid).OrderByDescending(r => r.CreatedAt).ToListAsync());
}
