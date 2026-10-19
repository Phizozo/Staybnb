using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Guest-only area: a Host (or Admin) cannot reach these pages.
[Authorize(Roles = "Guest")]
public class GuestController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly FileStorage _files;
    private readonly PropertyApplicationService _apps;
    private readonly ActivityLogger _log;
    private readonly NotificationService _notify;
    private readonly CheckInService _checkIn;

    public GuestController(ApplicationDbContext db, FileStorage files, PropertyApplicationService apps, ActivityLogger log, NotificationService notify, CheckInService checkIn)
    { _db = db; _files = files; _apps = apps; _log = log; _notify = notify; _checkIn = checkIn; }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ---------- Become a Host ----------
    [HttpGet]
    public async Task<IActionResult> Apply()
    {
        if (await _db.HostApplications.AnyAsync(a => a.ApplicationUserId == Uid && a.Status == ApplicationStatus.Pending))
        {
            TempData["Error"] = "You already have an application awaiting review.";
            return RedirectToAction(nameof(Applications));
        }
        ViewBag.Amenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
        return View(new PropertyFormVM());
    }

    [HttpPost]
    public async Task<IActionResult> Apply(PropertyFormVM vm)
    {
        if (await _db.HostApplications.AnyAsync(a => a.ApplicationUserId == Uid && a.Status == ApplicationStatus.Pending))
            return RedirectToAction(nameof(Applications));

        var imgError = _apps.ValidateImages(vm);
        if (imgError != null) ModelState.AddModelError("Images", imgError);
        if (!ModelState.IsValid)
        {
            ViewBag.Amenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
            return View(vm);
        }
        await _apps.SubmitAsync(Uid, vm, additional: false);
        TempData["Success"] = "Application submitted! An admin will review it shortly.";
        return RedirectToAction(nameof(Applications));
    }

    public async Task<IActionResult> Applications() =>
        View(await _db.HostApplications.Include(a => a.Property).ThenInclude(p => p.Images)
            .Where(a => a.ApplicationUserId == Uid).OrderByDescending(a => a.AppliedAt).ToListAsync());

    // ---------- Booking engine ----------
    [HttpPost]
    public async Task<IActionResult> Book(int propertyId, DateTime checkIn, DateTime checkOut, int guests)
    {
        var p = await _db.HostProperties.FirstOrDefaultAsync(x => x.Id == propertyId && x.IsActive);
        if (p == null) return NotFound();

        string err = null;
        if (checkIn.Date < DateTime.Today) err = "Check-in cannot be in the past.";
        else if (checkOut.Date <= checkIn.Date) err = "Check-out must be after check-in.";
        else if (guests < 1 || guests > p.MaxGuests) err = $"This property sleeps between 1 and {p.MaxGuests} guests.";
        else if (await _db.Bookings.AnyAsync(b => b.PropertyId == propertyId && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn)
                 && b.CheckInDate < checkOut.Date && b.CheckOutDate > checkIn.Date))
            err = "Those dates are not available.";
        if (err != null)
        {
            TempData["Error"] = err;
            return RedirectToAction("Details", "Properties", new { id = propertyId });
        }

        var nights = (checkOut.Date - checkIn.Date).Days;
        var q = BookingCalculator.Calculate(p.PricePerNight, p.CleaningFee, nights);
        var booking = new Booking
        {
            PropertyId = propertyId, GuestId = Uid, CheckInDate = checkIn.Date, CheckOutDate = checkOut.Date, NumberOfGuests = guests,
            CleaningFee = q.CleaningFee, ServiceFee = q.ServiceFee, TotalPrice = q.Total, Status = BookingStatus.Pending,
            Payment = new Payment { Amount = q.Total, Status = PaymentStatus.Pending }
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        await _log.LogAsync(Uid, "BookingRequested", ActivityType.BookingUpdate, $"Booking #{booking.Id} for '{p.Title}' (Pending)");
        await _notify.SendAsync(p.HostId, "New Booking Request", $"A guest requested '{p.Title}' for {nights} night(s).", NotificationType.Booking, "/Host/Bookings");
        await _notify.SendAsync(Uid, "Booking Requested", $"Your request for '{p.Title}' was sent to the host.", NotificationType.Booking, "/Guest/Bookings");
        TempData["Success"] = "Booking request sent! You'll be notified when the host responds.";
        return RedirectToAction(nameof(Bookings));
    }

    public async Task<IActionResult> Bookings() =>
        View(await _db.Bookings.Include(b => b.Property).ThenInclude(p => p.Images).Include(b => b.Property).ThenInclude(p => p.Host)
            .Include(b => b.Payment).Include(b => b.GuestCheckIn)
            .Where(b => b.GuestId == Uid).OrderByDescending(b => b.CreatedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var b = await _db.Bookings.Include(x => x.Property).Include(x => x.Payment).FirstOrDefaultAsync(x => x.Id == id && x.GuestId == Uid);
        if (b == null) return NotFound();
        if (b.Status != BookingStatus.Pending && b.Status != BookingStatus.Approved)
        {
            TempData["Error"] = "This booking can no longer be cancelled.";
            return RedirectToAction(nameof(Bookings));
        }
        b.Status = BookingStatus.Cancelled;
        if (b.Payment?.Status == PaymentStatus.Paid) b.Payment.Status = PaymentStatus.Refunded;
        await _db.SaveChangesAsync();
        await _log.LogAsync(Uid, "BookingCancelled", ActivityType.BookingUpdate, $"Booking #{b.Id} (Cancelled)");
        await _notify.SendAsync(b.Property.HostId, "Booking Cancelled", $"A guest cancelled their booking for '{b.Property.Title}'.", NotificationType.Warning, "/Host/Bookings");
        TempData["Success"] = "Booking cancelled.";
        return RedirectToAction(nameof(Bookings));
    }

    // ---------- Multi-step check-in ----------
    private Task<Booking> LoadBooking(int id) =>
        _db.Bookings.Include(b => b.Property).FirstOrDefaultAsync(b => b.Id == id && b.GuestId == Uid);

    public async Task<IActionResult> CheckIn(int bookingId)
    {
        var booking = await LoadBooking(bookingId);
        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Approved && booking.Status != BookingStatus.CheckedIn)
        {
            TempData["Error"] = "Check-in opens once the host has approved your booking.";
            return RedirectToAction(nameof(Bookings));
        }
        var proc = await _checkIn.EnsureProcessAsync(booking.PropertyId, booking.Property.Title);
        var ci = await _db.GuestCheckIns.Include(c => c.Documents).FirstOrDefaultAsync(c => c.BookingId == bookingId);
        if (ci == null)
        {
            ci = new GuestCheckIn { BookingId = bookingId, CheckInProcessId = proc.Id, Status = CheckInStatus.Pending };
            _db.GuestCheckIns.Add(ci);
            await _db.SaveChangesAsync();
        }
        return View(new GuestCheckInVM { Booking = booking, CheckIn = ci, Config = proc.Config, ProcessTitle = proc.Title });
    }

    // Step 1: accept the house rules
    [HttpPost]
    public async Task<IActionResult> AcceptRules(int bookingId)
    {
        var ci = await GetCheckIn(bookingId);
        if (ci == null) return NotFound();
        if (ci.Status == CheckInStatus.Pending)
        {
            ci.Status = CheckInStatus.RulesAccepted;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(CheckIn), new { bookingId });
    }

    // Step 2: upload the required documents (ID or Passport)
    [HttpPost]
    public async Task<IActionResult> UploadDocument(int bookingId, string documentType, IFormFile file)
    {
        var ci = await GetCheckIn(bookingId, includeProcess: true);
        if (ci == null) return NotFound();
        if (ci.Status != CheckInStatus.RulesAccepted && ci.Status != CheckInStatus.Rejected)
        {
            TempData["Error"] = "Documents can't be changed at this stage.";
            return RedirectToAction(nameof(CheckIn), new { bookingId });
        }
        if (!ci.CheckInProcess.Config.Documents.Contains(documentType))
        { TempData["Error"] = "Unknown document type."; return RedirectToAction(nameof(CheckIn), new { bookingId }); }
        if (!FileStorage.IsValidDocument(file))
        { TempData["Error"] = "Upload a JPG, PNG or PDF under 5MB."; return RedirectToAction(nameof(CheckIn), new { bookingId }); }

        var old = ci.Documents.Where(d => d.DocumentType == documentType).ToList();
        _db.GuestDocuments.RemoveRange(old); // a re-upload replaces the previous (e.g. rejected) file
        _db.GuestDocuments.Add(new GuestDocument
        {
            GuestCheckInId = ci.Id, DocumentType = documentType, FileName = await _files.SaveDocumentAsync(file),
            OriginalName = Path.GetFileName(file.FileName), Status = DocumentStatus.Pending
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(CheckIn), new { bookingId });
    }

    // Step 3: submit for host verification
    [HttpPost]
    public async Task<IActionResult> SubmitCheckIn(int bookingId)
    {
        var ci = await GetCheckIn(bookingId, includeProcess: true);
        if (ci == null) return NotFound();
        var required = ci.CheckInProcess.Config.Documents;
        var missing = required.Where(r => !ci.Documents.Any(d => d.DocumentType == r)).ToList();
        if (ci.Status != CheckInStatus.RulesAccepted && ci.Status != CheckInStatus.Rejected) return RedirectToAction(nameof(CheckIn), new { bookingId });
        if (missing.Any() || ci.Documents.Any(d => d.Status == DocumentStatus.Rejected))
        {
            TempData["Error"] = "Upload every required document (replace any rejected ones) before submitting.";
            return RedirectToAction(nameof(CheckIn), new { bookingId });
        }
        ci.Status = CheckInStatus.Submitted;
        await _db.SaveChangesAsync();
        var booking = await LoadBooking(bookingId);
        await _log.LogAsync(Uid, "CheckInSubmitted", ActivityType.CheckIn, $"Booking #{bookingId}");
        await _notify.SendAsync(booking.Property.HostId, "Guest Check-in Submitted", $"Documents for '{booking.Property.Title}' are ready to verify.", NotificationType.Booking, "/Host/CheckIns");
        TempData["Success"] = "Check-in submitted. The host will verify your documents.";
        return RedirectToAction(nameof(CheckIn), new { bookingId });
    }

    private Task<GuestCheckIn> GetCheckIn(int bookingId, bool includeProcess = false)
    {
        var q = _db.GuestCheckIns.Include(c => c.Documents).Include(c => c.Booking).AsQueryable();
        if (includeProcess) q = q.Include(c => c.CheckInProcess);
        return q.FirstOrDefaultAsync(c => c.BookingId == bookingId && c.Booking.GuestId == Uid);
    }

    // ---------- Wishlist & reviews ----------
    [HttpPost]
    public async Task<IActionResult> ToggleWishlist(int propertyId, string returnUrl)
    {
        var item = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == Uid && w.PropertyId == propertyId);
        if (item != null) _db.WishlistItems.Remove(item);
        else if (await _db.HostProperties.AnyAsync(p => p.Id == propertyId && p.IsActive)) _db.WishlistItems.Add(new WishlistItem { UserId = Uid, PropertyId = propertyId });
        await _db.SaveChangesAsync();
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Wishlist));
    }

    public async Task<IActionResult> Wishlist() =>
        View(await _db.WishlistItems.Include(w => w.Property).ThenInclude(p => p.Images).Where(w => w.UserId == Uid).OrderByDescending(w => w.CreatedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Review(int propertyId, int rating, string comment)
    {
        var eligible = await _db.Bookings.AnyAsync(b => b.GuestId == Uid && b.PropertyId == propertyId && (b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed));
        if (!eligible || rating < 1 || rating > 5 || await _db.Reviews.AnyAsync(r => r.ReviewerId == Uid && r.PropertyId == propertyId))
            TempData["Error"] = "You can't review this property.";
        else
        {
            _db.Reviews.Add(new Review { PropertyId = propertyId, ReviewerId = Uid, Rating = rating, Comment = comment });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Thanks for your review!";
        }
        return RedirectToAction("Details", "Properties", new { id = propertyId });
    }
}
