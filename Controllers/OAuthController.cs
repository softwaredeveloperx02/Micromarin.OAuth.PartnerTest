using Micromarin.OAuth.PartnerTest.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Micromarin.OAuth.PartnerTest.Controllers;

public class OAuthController : Controller
{
    private readonly PartnerOAuthOptions _oauth;

    public OAuthController(IOptionsSnapshot<PartnerOAuthOptions> oauth)
    {
        _oauth = oauth.Value;
    }

    [HttpGet("/login/micromarin")]
    public IActionResult LoginWithMicromarin()
    {
        if (string.IsNullOrWhiteSpace(_oauth.IdentityIssuerBaseUrl))
            return Content("PartnerOAuth: IdentityIssuerBaseUrl must be configured in appsettings.", "text/plain; charset=utf-8");

        var callbackUrl = $"{Request.Scheme}://{Request.Host}{_oauth.CallbackPath}";

        var issuer = "https://developer-application-identity.azurewebsites.net/";// _oauth.IdentityIssuerBaseUrl.TrimEnd('/');
        var pairs = new List<KeyValuePair<string, string?>>
        {
            new("partner_client_id", _oauth.ClientId),
            new("partner_client_secret", _oauth.ClientSecret),
            new("partner_redirect_uri", callbackUrl),
            new("partner_scope", _oauth.Scope),
        };

        var loginUrl = QueryHelpers.AddQueryString(issuer + "/login", pairs);
        return Redirect(loginUrl);
    }

    [HttpGet("/oauth/callback")]
    public IActionResult Callback(
        string? sso_status,
        string? account_id,
        string? client_id,
        string? ts,
        string? first_name,
        string? last_name,
        string? company_name,
        string? sig)
    {
        if (!string.Equals(sso_status, "success", StringComparison.Ordinal))
            return View("OAuthResult", new OAuthResultVm { Success = false, Message = "Partner SSO failed." });

        if (string.IsNullOrWhiteSpace(account_id) ||
            string.IsNullOrWhiteSpace(client_id) ||
            string.IsNullOrWhiteSpace(ts) ||
            string.IsNullOrWhiteSpace(sig))
        {
            return View("OAuthResult", new OAuthResultVm { Success = false, Message = "Required callback parameters are missing." });
        }

        var first = first_name ?? string.Empty;
        var last = last_name ?? string.Empty;
        var company = company_name ?? string.Empty;
        var payload = $"{account_id}.{client_id}.{ts}.{first}.{last}.{company}";
        var expected = ComputeSignature(payload, _oauth.ClientSecret ?? string.Empty);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(sig)))
        {
            return View("OAuthResult", new OAuthResultVm { Success = false, Message = "Callback signature verification failed." });
        }

        if (!long.TryParse(ts, out var tsLong))
        {
            return View("OAuthResult", new OAuthResultVm { Success = false, Message = "Invalid callback timestamp." });
        }
        var age = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - tsLong);
        if (age > 300)
        {
            return View("OAuthResult", new OAuthResultVm { Success = false, Message = "Callback link expired." });
        }

        var profile = new UserProfileVm
        {
            FirstName = string.IsNullOrWhiteSpace(first) ? "—" : first,
            LastName = string.IsNullOrWhiteSpace(last) ? "—" : last,
            CompanyName = string.IsNullOrWhiteSpace(company) ? "Micromarin user" : company,
            LoginSource = "Micromarin SSO",
            Email = null,
            AccountId = account_id
        };

        HttpContext.Session.SetString("UserProfile", System.Text.Json.JsonSerializer.Serialize(profile));

        return RedirectToAction("Profile", "Home");
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret ?? string.Empty));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

public sealed class OAuthResultVm
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
