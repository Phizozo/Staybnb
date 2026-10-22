using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize]
public class MessagesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly NotificationService _notify;
    private readonly ActivityLogger _log;
    public MessagesController(ApplicationDbContext db, NotificationService notify, ActivityLogger log) { _db = db; _notify = notify; _log = log; }
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<IActionResult> Index()
    {
        var uid = Uid;
        var msgs = await _db.Messages.Include(m => m.Sender).Include(m => m.Receiver)
            .Where(m => m.SenderId == uid || m.ReceiverId == uid).OrderByDescending(m => m.Timestamp).ToListAsync();
        var convs = msgs.GroupBy(m => m.SenderId == uid ? m.ReceiverId : m.SenderId).Select(g =>
        {
            var last = g.First();
            return new ConversationVM
            {
                Partner = last.SenderId == uid ? last.Receiver : last.Sender,
                Last = last,
                Unread = g.Count(m => m.ReceiverId == uid && !m.IsRead)
            };
        }).OrderByDescending(c => c.Last.Timestamp).ToList();
        return View(convs);
    }

    [HttpGet]
    public async Task<IActionResult> Thread(string id)
    {
        var partner = await _db.Users.FindAsync(id);
        if (partner == null || id == Uid) return NotFound();
        var uid = Uid;
        var list = await _db.Messages.Where(m => (m.SenderId == uid && m.ReceiverId == id) || (m.SenderId == id && m.ReceiverId == uid))
            .OrderBy(m => m.Timestamp).ToListAsync();
        var unread = list.Where(m => m.ReceiverId == uid && !m.IsRead).ToList();
        unread.ForEach(m => m.IsRead = true);           // read status persisted in the database
        if (unread.Any()) await _db.SaveChangesAsync();
        return View(new ThreadVM { Partner = partner, Messages = list });
    }

    [HttpPost]
    public async Task<IActionResult> Send(string receiverId, string content)
    {
        if (string.IsNullOrWhiteSpace(content) || receiverId == Uid || await _db.Users.FindAsync(receiverId) == null)
        {
            TempData["Error"] = "Message could not be sent.";
            return RedirectToAction(nameof(Index));
        }
        _db.Messages.Add(new Message { SenderId = Uid, ReceiverId = receiverId, Content = content.Trim() });
        await _db.SaveChangesAsync();
        var sender = await _db.Users.FindAsync(Uid);
        await _notify.SendAsync(receiverId, "New Message", $"You have a new message from {sender.FullName}.", NotificationType.Message, "/Messages/Thread/" + Uid);
        await _log.LogAsync(Uid, "MessageSent", ActivityType.Message, "To " + receiverId);
        return RedirectToAction(nameof(Thread), new { id = receiverId });
    }
}
