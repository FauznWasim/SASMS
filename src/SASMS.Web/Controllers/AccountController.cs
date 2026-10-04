using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SASMS.Web.Data;
using SASMS.Web.Models.Entities;
using SASMS.Web.Models.Enums;
using SASMS.Web.Models.ViewModels;
using SASMS.Web.Security;
using SASMS.Web.Services;

namespace SASMS.Web.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;
    private readonly AccountLockoutSettings _lockoutSettings;

    public AccountController(
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        IAuditService auditService,
        ICurrentUserService currentUser,
        IOptions<AccountLockoutSettings> lockoutSettings)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _auditService = auditService;
        _currentUser = currentUser;
        _lockoutSettings = lockoutSettings.Value;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == model.Username);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        if (user.LockoutEndUtc is not null && user.LockoutEndUtc > DateTime.UtcNow)
        {
            ModelState.AddModelError(string.Empty, "This account is temporarily locked due to failed login attempts. Try again later.");
            return View(model);
        }

        if (user.Status != UserStatus.Active)
        {
            ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact your Manager/PIC for assistance.");
            return View(model);
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= _lockoutSettings.MaxFailedAttempts)
            {
                user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(_lockoutSettings.LockoutMinutes);
                user.FailedLoginCount = 0;
            }
            await _db.SaveChangesAsync();
            await _auditService.LogAsync(user.UserId, "LoginFailed", "User", user.UserId.ToString(), null, ipAddress);

            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
        }
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.RoleName),
            new(AppClaimTypes.AuthTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };
        if (user.EmployeeId is not null)
        {
            claims.Add(new Claim(AppClaimTypes.EmployeeId, user.EmployeeId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });

        await _auditService.LogAsync(user.UserId, "LoginSuccess", "User", user.UserId.ToString(), null, ipAddress);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _auditService.LogAsync(_currentUser.UserId, "Logout", "User", _currentUser.UserId.ToString(), null, _currentUser.IpAddress);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _db.Users.FindAsync(_currentUser.UserId);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
            return View(model);
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(_currentUser.UserId, "ChangePassword", "User", _currentUser.UserId.ToString(), null, _currentUser.IpAddress);

        TempData["StatusMessage"] = "Password changed successfully.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
