using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Micromarin.OAuth.PartnerTest.Models;

namespace Micromarin.OAuth.PartnerTest.Controllers;

public class HomeController : Controller
{
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
            return RedirectToAction(nameof(DemoWelcome), new { username = email });
        }

        TempData["LoginError"] = "Kullanıcı adı veya şifre yanlış";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult DemoWelcome(string username)
    {
        ViewBag.Username = string.IsNullOrWhiteSpace(username) ? "Kullanıcı Adı" : username;
        return View();
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
