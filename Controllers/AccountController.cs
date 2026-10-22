using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _um;
    private readonly SignInManager<ApplicationUser> _sm;
    private readonly ActivityLogger _log;
    private readonly NotificationService _notify;

    public AccountController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, ActivityLogger log, NotificationService notify)
    { _um = um; _sm = sm; _log = log; _notify = notify; }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterVM());

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterVM vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = new ApplicationUser { UserName = vm.Email, Email = vm.Email, FirstName = vm.FirstName, LastName = vm.LastName, EmailConfirmed = true };
        var result = await _um.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }
        await _um.AddToRoleAsync(user, Roles.Guest); // every new registrant starts as a Guest
        await _log.LogAsync(user.Id, "UserRegistered", ActivityType.Registration, user.Email);
        await _notify.SendAsync(user.Id, "Welcome to Staybnb", "Browse stays, or apply to become a Host.", NotificationType.Info, "/Properties");
        await _sm.SignInAsync(user, false);
        return RedirectToAction("Index", "Properties");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string returnUrl = null) => View(new LoginVM { ReturnUrl = returnUrl });

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginVM vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = await _um.FindByEmailAsync(vm.Email);
        if (user != null)
        {
            var res = await _sm.PasswordSignInAsync(user.UserName, vm.Password, vm.RememberMe, lockoutOnFailure: false);
            if (res.Succeeded)
            {
                await _log.LogAsync(user.Id, "UserLoggedIn", ActivityType.Login, user.Email);
                if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) return LocalRedirect(vm.ReturnUrl);
                return await RedirectByRole(user);
            }
            await _log.LogAsync(user.Id, "FailedLogin", ActivityType.Login, user.Email);
        }
        ModelState.AddModelError("", "Invalid email or password.");
        return View(vm);
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        var uid = _um.GetUserId(User);
        await _log.LogAsync(uid, "UserLoggedOut", ActivityType.Logout);
        await _sm.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task<IActionResult> RedirectByRole(ApplicationUser user)
    {
        var roles = await _um.GetRolesAsync(user);
        if (roles.Contains(Roles.Admin) || roles.Contains(Roles.SuperAdmin)) return RedirectToAction("Dashboard", "Admin");
        if (roles.Contains(Roles.Host)) return RedirectToAction("Dashboard", "Host");
        return RedirectToAction("Index", "Properties");
    }
}
