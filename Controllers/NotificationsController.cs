using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;

namespace Staybnb.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    public NotificationsController(ApplicationDbContext db) => _db = db;
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<IActionResult> Index(string filter = "all", int? selectedId = null)
    {
        var all = await _db.Notifications.Where(n => n.UserId == Uid).OrderByDescending(n => n.CreatedAt).ToListAsync();
        ViewBag.All = all.Count; ViewBag.Unread = all.Count(n => !n.IsRead); ViewBag.Read = all.Count(n => n.IsRead);
        ViewBag.Filter = filter;
        var list = filter == "unread" ? all.Where(n => !n.IsRead).ToList() : filter == "read" ? all.Where(n => n.IsRead).ToList() : all;

        var selected = selectedId.HasValue ? all.FirstOrDefault(n => n.Id == selectedId) : null;
        if (selected != null && !selected.IsRead)
        {
            selected.IsRead = true; // opening a notification marks it as read in the database
            await _db.SaveChangesAsync();
            ViewBag.Unread = all.Count(n => !n.IsRead); ViewBag.Read = all.Count(n => n.IsRead);
        }
        ViewBag.Selected = selected;
        return View(list);
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(int id)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid);
        if (n != null) { n.IsRead = true; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        await _db.Notifications.Where(n => n.UserId == Uid && !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid);
        if (n != null) { _db.Notifications.Remove(n); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}
