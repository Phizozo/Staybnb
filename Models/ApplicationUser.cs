using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Staybnb.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(50)] public string FirstName { get; set; }
    [Required, StringLength(50)] public string LastName { get; set; }
    public string ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string FullName => $"{FirstName} {LastName}";
}
