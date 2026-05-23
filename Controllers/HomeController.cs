using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Micromarin.OAuth.PartnerTest.Models;

namespace Micromarin.OAuth.PartnerTest.Controllers;

public class HomeController : Controller
{
    public IActionResult Index(string denied, string ssoError)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Profile));

        if (string.Equals(denied, "1", StringComparison.Ordinal))
            ViewBag.SsoCancelled = true;
        else if (string.Equals(ssoError, "1", StringComparison.Ordinal))
            ViewBag.SsoError = true;

        return View();
    }

    [HttpGet("/login/micromarin")]
    public IActionResult LoginWithMicromarin()
    {
        return Challenge(
            new AuthenticationProperties { RedirectUri = Url.Action(nameof(Profile)) },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost]
    public async Task<IActionResult> DemoLogin(string email, string password)
    {
        if (string.Equals(email, "Admin", StringComparison.Ordinal) &&
            string.Equals(password, "Admin", StringComparison.Ordinal))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "demo-admin"),
                new(ClaimTypes.GivenName, "Demo"),
                new(ClaimTypes.Surname, "Admin"),
                new("preferred_username", "admin"),
                new("company_name", "Partner Test Co."),
                new("company_title", "Administrator"),
                new("login_source", "Local demo login"),
                new(ClaimTypes.Email, "admin@partner.test"),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });

            return RedirectToAction(nameof(Profile));
        }

        TempData["LoginError"] = "Kullanıcı adı veya şifre yanlış";
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpGet]
    public IActionResult Profile()
    {
        var profile = UserProfileVm.FromClaims(User);
        return View(profile);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
