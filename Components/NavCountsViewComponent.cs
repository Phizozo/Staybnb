using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.ViewModels;

namespace Staybnb.Components;

public class NavCountsViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    public NavCountsViewComponent(ApplicationDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var uid = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var vm = new NavCountsVM
        {
            Notifications = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead),
            Messages = await _db.Messages.CountAsync(m => m.ReceiverId == uid && !m.IsRead)
        };
        return View(vm);
    }
}
