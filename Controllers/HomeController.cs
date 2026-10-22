using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;

namespace Staybnb.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.SuperAdmin)) return RedirectToAction("Dashboard", "Admin");
        if (User.IsInRole(Roles.Host)) return RedirectToAction("Dashboard", "Host");
        return RedirectToAction("Index", "Properties");
    }

    public IActionResult Error() => View();
}
