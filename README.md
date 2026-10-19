# Staybnb - Airbnb Clone (ASP.NET Core 8 MVC + EF Core + SQL Server)

## Run it
1. Install the .NET 8 SDK and SQL Server (LocalDB ships with Visual Studio).
2. Edit `ConnectionStrings:DefaultConnection` in `appsettings.json` if you don't use LocalDB.
3. Create the migration (recommended - the assignment marks EF migrations):
   ```
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
   (If no migration exists, the app falls back to `EnsureCreated()` on startup. Do not mix the two on one database.)
4. `dotnet run` - on startup `RoleSeeder` creates the roles, amenities and the SuperAdmin.

**SuperAdmin login:** `superadmin@staybnb.com` / `SuperAdmin@123` (change in `appsettings.json` -> `SeedSuperAdmin`).

## Demo script (covers the rubric)
1. Register two users (both become **Guest**). Log in as one -> *Become a Host*, fill the form. Submitting without an image is blocked.
2. Log in as the SuperAdmin -> *Host Requests* -> Approve. The user gets **Host added, Guest removed**, an activity-log entry and a notification.
3. SuperAdmin -> *Users* -> promote the second Guest to **Admin** (only SuperAdmin sees this page).
4. The new Host signs in again (security stamp was refreshed) -> Host dashboard. Configure the *Check-in process* on a property.
5. Guest 2... use a third Guest account: browse/filter, pick dates (live price), request booking. Host -> *Booking Requests* -> Approve.
6. Guest -> *My Bookings* -> Check in (rules -> upload ID -> submit). Host -> *Guest Check-ins* -> Verify. Booking becomes **CheckedIn**.
7. Messages (inbox/thread), bell notifications (read/unread stored in DB) and Admin -> *Activity Logs*.

## Architecture
- `Models/` entities matching the ERD (+ `IsRead`, `Link`, etc.). `Data/ApplicationDbContext` holds all relationships (1-1, 1-many, many-to-many Property<->Amenity) with Restrict deletes to avoid SQL Server cascade cycles.
- `Controllers/` Account, Home, Properties (public), Guest, Host, Admin, Messages, Notifications, Documents. Role access via `[Authorize(Roles = "...")]`.
- `Services/` `BookingCalculator` (TotalPrice = PricePerNight x Nights + CleaningFee + ServiceFee @ 12%), `ActivityLogger`, `NotificationService`, `PropertyApplicationService`, `CheckInService`, `FileStorage`.
- Guest ID documents are stored in `App_Data/documents` (outside wwwroot) and served only to the guest or the property's host.
