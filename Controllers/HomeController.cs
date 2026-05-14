using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Micromarin.OAuth.PartnerTest.Models;

namespace Micromarin.OAuth.PartnerTest.Controllers;

public class HomeController : Controller
{
    private const string ProfileSessionKey = "UserProfile";

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public IActionResult DemoLogin(string email, string password)
    {
        if (string.Equals(email, "Admin", StringComparison.Ordinal) &&
            string.Equals(password, "Admin", StringComparison.Ordinal))
        {
            var profile = new UserProfileVm
            {
                FirstName = "Demo",
                LastName = "Admin",
                UserName = "admin",
                CompanyName = "Partner Test Co.",
                CompanyTitle = "Administrator",
                LoginSource = "Local demo login",
                Email = "admin@partner.test",
                PartnerSub = null
            };

            HttpContext.Session.SetString(
                ProfileSessionKey,
                System.Text.Json.JsonSerializer.Serialize(profile));

            return RedirectToAction(nameof(Profile));
        }

        TempData["LoginError"] = "Kullanıcı adı veya şifre yanlış";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Profile()
    {
        var raw = HttpContext.Session.GetString(ProfileSessionKey);

        if (string.IsNullOrWhiteSpace(raw))
            return RedirectToAction(nameof(Index));

        var profile = System.Text.Json.JsonSerializer.Deserialize<UserProfileVm>(raw)
                      ?? new UserProfileVm();

        return View(profile);
    }

    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(ProfileSessionKey);
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
