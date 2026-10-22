using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<HostProperty> HostProperties => Set<HostProperty>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<HostApplication> HostApplications => Set<HostApplication>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<CheckInProcess> CheckInProcesses => Set<CheckInProcess>();
    public DbSet<GuestCheckIn> GuestCheckIns => Set<GuestCheckIn>();
    public DbSet<GuestDocument> GuestDocuments => Set<GuestDocument>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // One user -> many properties / bookings / reviews (Restrict avoids SQL Server multiple cascade paths)
        b.Entity<HostProperty>().HasOne(p => p.Host).WithMany().HasForeignKey(p => p.HostId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Booking>().HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Booking>().HasOne(x => x.Property).WithMany(p => p.Bookings).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Review>().HasOne(r => r.Reviewer).WithMany().HasForeignKey(r => r.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Review>().HasOne(r => r.Property).WithMany(p => p.Reviews).HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.Cascade);

        // One-to-one relationships
        b.Entity<Booking>().HasOne(x => x.Payment).WithOne(p => p.Booking).HasForeignKey<Payment>(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Booking>().HasOne(x => x.GuestCheckIn).WithOne(c => c.Booking).HasForeignKey<GuestCheckIn>(c => c.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HostProperty>().HasOne(p => p.CheckInProcess).WithOne(c => c.Property).HasForeignKey<CheckInProcess>(c => c.PropertyId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<GuestCheckIn>().HasOne(c => c.CheckInProcess).WithMany().HasForeignKey(c => c.CheckInProcessId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GuestDocument>().HasOne(d => d.GuestCheckIn).WithMany(c => c.Documents).HasForeignKey(d => d.GuestCheckInId).OnDelete(DeleteBehavior.Cascade);

        // Many-to-many Property <-> Amenity
        b.Entity<HostProperty>().HasMany(p => p.Amenities).WithMany(a => a.Properties);

        b.Entity<PropertyImage>().HasOne(i => i.Property).WithMany(p => p.Images).HasForeignKey(i => i.PropertyId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<HostApplication>().HasOne(a => a.ApplicationUser).WithMany().HasForeignKey(a => a.ApplicationUserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HostApplication>().HasOne(a => a.Property).WithMany().HasForeignKey(a => a.PropertyId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<WishlistItem>().HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<WishlistItem>().HasOne(w => w.Property).WithMany(p => p.WishlistItems).HasForeignKey(w => w.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WishlistItem>().HasIndex(w => new { w.UserId, w.PropertyId }).IsUnique();

        b.Entity<Message>().HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Message>().HasOne(m => m.Receiver).WithMany().HasForeignKey(m => m.ReceiverId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Message>().HasIndex(m => new { m.ReceiverId, m.IsRead });

        b.Entity<Notification>().HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead });

        b.Entity<ActivityLog>().HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<ActivityLog>().HasIndex(a => a.CreatedAt);
    }
}
