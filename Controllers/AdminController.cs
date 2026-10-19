using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _um;
    private readonly ActivityLogger _log;
    private readonly NotificationService _notify;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> um, ActivityLogger log, NotificationService notify)
    { _db = db; _um = um; _log = log; _notify = notify; }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<IActionResult> Dashboard()
    {
        var vm = new AdminDashboardVM
        {
            TotalUsers = await _db.Users.CountAsync(),
            PendingApps = await _db.HostApplications.CountAsync(a => a.Status == ApplicationStatus.Pending),
            ActiveProperties = await _db.HostProperties.CountAsync(p => p.IsActive),
            Revenue = await _db.Payments.Where(p => p.Status == PaymentStatus.Paid).SumAsync(p => (decimal?)p.Amount) ?? 0,
            TotalBookings = await _db.Bookings.CountAsync(),
            PendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending),
            ApprovedHosts = (await _um.GetUsersInRoleAsync(Roles.Host)).Count,
            Activity = await _db.ActivityLogs.Include(a => a.User).OrderByDescending(a => a.CreatedAt).Take(6).ToListAsync(),
            Applications = await _db.HostApplications.Include(a => a.ApplicationUser).Include(a => a.Property).OrderByDescending(a => a.AppliedAt).Take(5).ToListAsync(),
            Bookings = await _db.Bookings.Include(b => b.Guest).Include(b => b.Property).OrderByDescending(b => b.CreatedAt).Take(5).ToListAsync()
        };
        return View(vm);
    }

    // ---------- Application review ----------
    public async Task<IActionResult> Applications(string status = "Pending")
    {
        var q = _db.HostApplications.Include(a => a.ApplicationUser).Include(a => a.Property).AsQueryable();
        if (Enum.TryParse<ApplicationStatus>(status, out var s)) q = q.Where(a => a.Status == s);
        ViewBag.Status = status;
        return View(await q.OrderByDescending(a => a.AppliedAt).ToListAsync());
    }

    public async Task<IActionResult> Application(int id)
    {
        var a = await _db.HostApplications.Include(x => x.ApplicationUser).Include(x => x.Property).ThenInclude(p => p.Images)
            .Include(x => x.Property).ThenInclude(p => p.Amenities).FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        ViewBag.IsFirstApplication = !await _um.IsInRoleAsync(a.ApplicationUser, Roles.Host);
        return View(a);
    }

    [HttpPost]
    public async Task<IActionResult> ReviewApplication(int id, bool approve, string note)
    {
        var app = await _db.HostApplications.Include(a => a.ApplicationUser).Include(a => a.Property).FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();
        if (app.Status != ApplicationStatus.Pending)
        {
            TempData["Error"] = "This application was already reviewed.";
            return RedirectToAction(nameof(Applications));
        }
        var user = app.ApplicationUser;
        app.ReviewedAt = DateTime.UtcNow;
        app.AdminNote = note;

        if (approve)
        {
            app.Status = ApplicationStatus.Approved;
            app.Property.IsActive = true;
            // The transition: add Host, remove Guest (a user is a Guest OR a Host, never both)
            if (!await _um.IsInRoleAsync(user, Roles.Host))
            {
                await _um.AddToRoleAsync(user, Roles.Host);
                if (await _um.IsInRoleAsync(user, Roles.Guest)) await _um.RemoveFromRoleAsync(user, Roles.Guest);
                await _um.UpdateSecurityStampAsync(user); // forces a fresh sign-in so the new role is picked up
                await _log.LogAsync(Uid, "RoleChanged", ActivityType.RoleChange, $"{user.Email}: Guest -> Host");
            }
            await _db.SaveChangesAsync();
            await _notify.SendAsync(user.Id, "Host Application Approved", $"Congratulations! '{app.Property.Title}' has been approved. Sign in again to open your Host dashboard.", NotificationType.Success, "/Host/Dashboard");
        }
        else
        {
            app.Status = ApplicationStatus.Rejected;
            app.Property.IsActive = false;
            await _db.SaveChangesAsync();
            await _notify.SendAsync(user.Id, "Host Application Rejected", $"'{app.Property.Title}' was not approved. {note}", NotificationType.Warning, "/Guest/Applications");
        }
        await _log.LogAsync(Uid, approve ? "ApplicationApproved" : "ApplicationRejected", ActivityType.Application, $"{user.Email}: {app.Property.Title}");
        TempData["Success"] = approve ? "Application approved." : "Application rejected.";
        return RedirectToAction(nameof(Applications));
    }

    // ---------- User management (SuperAdmin only) ----------
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Users(string q)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u => u.Email.Contains(q) || u.FirstName.Contains(q) || u.LastName.Contains(q));
        var users = await query.OrderBy(u => u.FirstName).Take(100).ToListAsync();
        var rows = new List<UserRowVM>();
        foreach (var u in users) rows.Add(new UserRowVM { User = u, Roles = await _um.GetRolesAsync(u) });
        ViewBag.Query = q;
        return View(rows);
    }

    [HttpPost, Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Promote(string userId)
    {
        var user = await _um.FindByIdAsync(userId);
        if (user == null) return NotFound();
        if (!await _um.IsInRoleAsync(user, Roles.Guest))
        {
            TempData["Error"] = "Only Guests can be promoted to Admin.";
            return RedirectToAction(nameof(Users));
        }
        await _um.RemoveFromRoleAsync(user, Roles.Guest);
        await _um.AddToRoleAsync(user, Roles.Admin);
        await _um.UpdateSecurityStampAsync(user);
        await _log.LogAsync(Uid, "RoleChanged", ActivityType.RoleChange, $"{user.Email}: Guest -> Admin");
        await _notify.SendAsync(user.Id, "You are now an Admin", "A SuperAdmin promoted you. Sign in again to access the Admin dashboard.", NotificationType.Success, "/Admin/Dashboard");
        TempData["Success"] = $"{user.FullName} is now an Admin.";
        return RedirectToAction(nameof(Users));
    }

    // ---------- Oversight lists ----------
    public async Task<IActionResult> Bookings() =>
        View(await _db.Bookings.Include(b => b.Guest).Include(b => b.Property).Include(b => b.Payment).OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync());

    public async Task<IActionResult> Properties() =>
        View(await _db.HostProperties.Include(p => p.Host).Include(p => p.Images).OrderByDescending(p => p.CreatedAt).ToListAsync());

    public async Task<IActionResult> ActivityLogs(string type, int page = 1)
    {
        var q = _db.ActivityLogs.Include(a => a.User).AsQueryable();
        if (Enum.TryParse<ActivityType>(type, out var t)) q = q.Where(a => a.ActivityType == t);
        const int size = 25;
        var total = await q.CountAsync();
        ViewBag.Type = type; ViewBag.Page = page; ViewBag.Pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        return View(await q.OrderByDescending(a => a.CreatedAt).Skip((Math.Max(page, 1) - 1) * size).Take(size).ToListAsync());
    }
}
