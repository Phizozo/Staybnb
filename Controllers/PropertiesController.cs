using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Public browsing (no login needed) - booking itself is Guest-only (see GuestController).
public class PropertiesController : Controller
{
    private readonly ApplicationDbContext _db;
    public PropertiesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string city, string type)
    {
        var q = _db.HostProperties.Include(p => p.Images).Include(p => p.Reviews).Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(p => p.City.Contains(city));
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(p => p.PropertyType == type);

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var vm = new PropertyIndexVM
        {
            Properties = await q.OrderByDescending(p => p.CreatedAt).ToListAsync(),
            Cities = await _db.HostProperties.Where(p => p.IsActive).Select(p => p.City).Distinct().OrderBy(c => c).ToListAsync(),
            City = city, Type = type
        };
        if (uid != null)
            vm.WishlistIds = (await _db.WishlistItems.Where(w => w.UserId == uid).Select(w => w.PropertyId).ToListAsync()).ToHashSet();
        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var p = await _db.HostProperties.Include(x => x.Host).Include(x => x.Images).Include(x => x.Amenities)
            .Include(x => x.Reviews).ThenInclude(r => r.Reviewer).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isStaff = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.SuperAdmin) || p.HostId == uid;
        if (!p.IsActive && !isStaff) return NotFound();

        var vm = new PropertyDetailsVM { Property = p };
        var blocked = await _db.Bookings.Where(b => b.PropertyId == id && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) && b.CheckOutDate >= DateTime.Today).ToListAsync();
        vm.Unavailable = blocked.Select(b => $"{b.CheckInDate:yyyy-MM-dd} to {b.CheckOutDate:yyyy-MM-dd}").ToList();
        if (uid != null && User.IsInRole(Roles.Guest))
        {
            vm.IsWishlisted = await _db.WishlistItems.AnyAsync(w => w.UserId == uid && w.PropertyId == id);
            vm.CanReview = await _db.Bookings.AnyAsync(b => b.GuestId == uid && b.PropertyId == id && (b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed))
                           && !await _db.Reviews.AnyAsync(r => r.ReviewerId == uid && r.PropertyId == id);
        }
        return View(vm);
    }

    // Live price quote used by the booking widget: TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
    [HttpGet]
    public async Task<IActionResult> Quote(int id, DateTime checkIn, DateTime checkOut)
    {
        var p = await _db.HostProperties.FindAsync(id);
        if (p == null) return NotFound();
        var nights = (checkOut.Date - checkIn.Date).Days;
        if (nights < 1) return Json(new { ok = false, error = "Check-out must be after check-in." });
        var q = BookingCalculator.Calculate(p.PricePerNight, p.CleaningFee, nights);
        return Json(new { ok = true, nights = q.Nights, subtotal = q.Subtotal, cleaning = q.CleaningFee, service = q.ServiceFee, total = q.Total });
    }
}
