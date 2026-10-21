using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

// Ensures SuperAdmin, Host, Guest roles exist and default SuperAdmin user
//Logic: Check RoleExists -> Create role -> Assign Guest as default new user role

public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var cfg = sp.GetRequiredService<IConfiguration>();

        // Use migrations when they exist (dotnet ef migrations add InitialCreate), otherwise create the schema.
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var email = cfg["SeedSuperAdmin:Email"];
        var superAdmin = await um.FindByEmailAsync(email);
        if (superAdmin == null)
        {
            superAdmin = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                FirstName = cfg["SeedSuperAdmin:FirstName"] ?? "Super",
                LastName = cfg["SeedSuperAdmin:LastName"] ?? "Admin"
            };
            var result = await um.CreateAsync(superAdmin, cfg["SeedSuperAdmin:Password"]);
            if (!result.Succeeded)
                throw new InvalidOperationException("SuperAdmin seeding failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        if (!await um.IsInRoleAsync(superAdmin, Roles.SuperAdmin)) await um.AddToRoleAsync(superAdmin, Roles.SuperAdmin);

        if (!await db.Amenities.AnyAsync())
        {
            db.Amenities.AddRange(
                new Amenity { Name = "Wifi", IconClass = "bi-wifi" },
                new Amenity { Name = "Kitchen", IconClass = "bi-egg-fried" },
                new Amenity { Name = "Washer", IconClass = "bi-basket" },
                new Amenity { Name = "Air Conditioning", IconClass = "bi-snow" },
                new Amenity { Name = "Heating", IconClass = "bi-thermometer-sun" },
                new Amenity { Name = "Pool", IconClass = "bi-water" },
                new Amenity { Name = "Free Parking", IconClass = "bi-car-front" },
                new Amenity { Name = "TV", IconClass = "bi-tv" },
                new Amenity { Name = "Braai Area", IconClass = "bi-fire" });
            await db.SaveChangesAsync();
        }
    }
}
