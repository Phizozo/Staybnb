using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Services;

namespace Staybnb.Controllers;

// Guest ID documents are private: only the uploading guest or the property's host may open them.
[Authorize]
public class DocumentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly FileStorage _files;
    public DocumentsController(ApplicationDbContext db, FileStorage files) { _db = db; _files = files; }

    public async Task<IActionResult> Download(int id)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var doc = await _db.GuestDocuments.Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();
        var b = doc.GuestCheckIn.Booking;
        if (b.GuestId != uid && b.Property.HostId != uid) return Forbid();
        var path = _files.DocumentPath(doc.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext == ".pdf" ? "application/pdf" : ext == ".png" ? "image/png" : "image/jpeg";
        return PhysicalFile(path, mime);
    }
}
