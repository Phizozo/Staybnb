using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public class CheckInService
{
    private readonly ApplicationDbContext _db;
    public CheckInService(ApplicationDbContext db) => _db = db;

    /// <summary>Every property has one CheckInProcess; a sensible default is created if the host hasn't configured one yet.</summary>
    public async Task<CheckInProcess> EnsureProcessAsync(int propertyId, string propertyTitle)
    {
        var proc = await _db.CheckInProcesses.FirstOrDefaultAsync(c => c.PropertyId == propertyId);
        if (proc == null)
        {
            proc = new CheckInProcess { PropertyId = propertyId, Title = $"Check-in for {propertyTitle}", StepsJson = CheckInConfig.Default().ToJson() };
            _db.CheckInProcesses.Add(proc);
            await _db.SaveChangesAsync();
        }
        return proc;
    }
}
