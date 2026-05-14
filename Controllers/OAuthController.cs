using Micromarin.OAuth.PartnerTest.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;

namespace Micromarin.OAuth.PartnerTest.Controllers;

public class OAuthController : Controller
{
    private const string SessionKeyState = "PartnerSso:State";
    private const string SessionKeyProfile = "UserProfile";

    private readonly PartnerOAuthOptions _oauth;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _discoveryManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OAuthController> _logger;

    public OAuthController(
        IOptionsSnapshot<PartnerOAuthOptions> oauth,
        IConfigurationManager<OpenIdConnectConfiguration> discoveryManager,
        IHttpClientFactory httpClientFactory,
        ILogger<OAuthController> logger)
    {
        _oauth = oauth.Value;
        _discoveryManager = discoveryManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("/login/micromarin")]
    public IActionResult LoginWithMicromarin()
    {
        if (string.IsNullOrWhiteSpace(_oauth.IdentityIssuerBaseUrl))
            return Content("PartnerOAuth: IdentityIssuerBaseUrl must be configured in appsettings.",
                "text/plain; charset=utf-8");
        if (string.IsNullOrWhiteSpace(_oauth.ClientId))
            return Content("PartnerOAuth: ClientId must be configured in appsettings.",
                "text/plain; charset=utf-8");

        var state = GenerateRandomToken();
        HttpContext.Session.SetString(SessionKeyState, state);

        var callbackUrl = $"{Request.Scheme}://{Request.Host}{_oauth.CallbackPath}";
        var issuer = _oauth.IdentityIssuerBaseUrl.TrimEnd('/');

        var pairs = new List<KeyValuePair<string, string?>>
        {
            new("partner_client_id", _oauth.ClientId),
            new("partner_redirect_uri", callbackUrl),
            new("partner_scope", _oauth.Scope),
            new("partner_state", state),
        };

        var loginUrl = QueryHelpers.AddQueryString(issuer + "/login", pairs);
        return Redirect(loginUrl);
    }

    [HttpGet("/oauth/callback")]
    public async Task<IActionResult> Callback(
        string? code,
        string? state,
        string? sso_status,
        string? error,
        CancellationToken cancellationToken)
    {
        var expectedState = HttpContext.Session.GetString(SessionKeyState);
        HttpContext.Session.Remove(SessionKeyState);

        if (string.IsNullOrWhiteSpace(expectedState) ||
            !FixedTimeStringEquals(expectedState, state))
        {
            _logger.LogWarning("Partner SSO callback: state mismatch (CSRF / stale session).");
            return Fail("State parameter mismatch. Please try signing in again.");
        }

        if (!string.IsNullOrEmpty(sso_status) &&
            !string.Equals(sso_status, "success", StringComparison.Ordinal))
        {
            var msg = string.IsNullOrWhiteSpace(error)
                ? "Sign-in was cancelled or failed."
                : $"Sign-in failed: {error}";
            return Fail(msg);
        }

        if (string.IsNullOrWhiteSpace(code))
            return Fail("Authorization code is missing.");

        TokenExchangeResponse? tokenResponse;
        try
        {
            tokenResponse = await ExchangeCodeForAssertionAsync(code, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Partner SSO callback: token exchange failed.");
            return Fail("Could not exchange authorization code for identity assertion.");
        }

        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.id_assertion))
            return Fail("Identity assertion was not returned by Micromarin.");

        ClaimsValidationResult validation;

        try
        {
            validation = await ValidateAssertionAsync(tokenResponse.id_assertion, cancellationToken);
        }
        catch (SecurityTokenException stex)
        {
            _logger.LogWarning(stex, "Partner SSO callback: JWT validation failed.");
            return Fail("Identity assertion is invalid: " + stex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Partner SSO callback: unexpected error while validating assertion.");
            return Fail("Identity assertion could not be validated.");
        }

        var profile = new UserProfileVm
        {
            FirstName = validation.GivenName ?? "",
            LastName = validation.FamilyName ?? "",
            UserName = validation.PreferredUsername ?? "",
            CompanyName = validation.CompanyName ?? "Micromarin user",
            CompanyTitle = validation.CompanyTitle ?? "",
            LoginSource = "Micromarin SSO",
            Email = null,
            PartnerSub = validation.Sub
        };

        HttpContext.Session.SetString(
            SessionKeyProfile,
            JsonSerializer.Serialize(profile));

        return RedirectToAction("Profile", "Home");
    }

    private async Task<TokenExchangeResponse?> ExchangeCodeForAssertionAsync(
        string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_oauth.ClientSecret))
            throw new InvalidOperationException("PartnerOAuth.ClientSecret is not configured.");

        var issuer = _oauth.IdentityIssuerBaseUrl.TrimEnd('/');
        var tokenUrl = issuer + (_oauth.TokenEndpointPath ?? "/partner-token");
        var callbackUrl = $"{Request.Scheme}://{Request.Host}{_oauth.CallbackPath}";

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", callbackUrl),
            new KeyValuePair<string, string>("client_id", _oauth.ClientId),
            new KeyValuePair<string, string>("client_secret", _oauth.ClientSecret),
        });

        using var http = _httpClientFactory.CreateClient("identity");
        http.Timeout = TimeSpan.FromSeconds(15);

        using var response = await http.PostAsync(tokenUrl, form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Partner SSO token exchange returned {Status}: {Body}",
                (int)response.StatusCode, body);

            try
            {
                var err = JsonSerializer.Deserialize<OAuthErrorResponse>(
                    body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                throw new InvalidOperationException(
                    $"Identity rejected token exchange: {err?.error} - {err?.error_description}");
            }
            catch (JsonException)
            {
                throw new InvalidOperationException(
                    $"Identity rejected token exchange (HTTP {(int)response.StatusCode}).");
            }
        }

        return JsonSerializer.Deserialize<TokenExchangeResponse>(
            body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private async Task<ClaimsValidationResult> ValidateAssertionAsync(
        string assertion, CancellationToken cancellationToken)
    {
        var oidcConfig = await _discoveryManager.GetConfigurationAsync(cancellationToken);

        var expectedIssuer = (_oauth.IdentityIssuerBaseUrl ?? "").TrimEnd('/');
        var expectedAudienceRaw = string.IsNullOrWhiteSpace(_oauth.ExpectedAudience)
            ? _oauth.ClientId
            : _oauth.ExpectedAudience;

        var expectedAudienceGuid = Guid.TryParse(expectedAudienceRaw, out var audGuid)
            ? audGuid
            : (Guid?)null;

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { expectedIssuer, expectedIssuer + "/", oidcConfig.Issuer }
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToArray(),

            ValidateAudience = true,
            ValidAudience = expectedAudienceRaw,
            AudienceValidator = (audiences, _, _) =>
            {
                if (audiences == null) return false;
                foreach (var a in audiences)
                {
                    if (string.IsNullOrWhiteSpace(a)) continue;

                    if (expectedAudienceGuid.HasValue &&
                        Guid.TryParse(a, out var tokenAud) &&
                        tokenAud == expectedAudienceGuid.Value)
                        return true;

                    if (string.Equals(a, expectedAudienceRaw, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            },

            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = oidcConfig.SigningKeys,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(Math.Max(0, _oauth.ClockSkewSeconds)),

            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
            RequireSignedTokens = true,
            RequireExpirationTime = true,
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(assertion, validationParameters, out _);

        var tokenUse = principal.FindFirst("token_use")?.Value;
        if (!string.Equals(tokenUse, "partner_assertion", StringComparison.Ordinal))
            throw new SecurityTokenException("Unexpected token_use claim.");

        return new ClaimsValidationResult(
            Sub: principal.FindFirst("sub")?.Value,
            GivenName: principal.FindFirst("given_name")?.Value,
            FamilyName: principal.FindFirst("family_name")?.Value,
            Name: principal.FindFirst("name")?.Value,
            PreferredUsername: principal.FindFirst("preferred_username")?.Value,
            CompanyName: principal.FindFirst("company_name")?.Value,
            CompanyTitle: principal.FindFirst("company_title")?.Value);
    }

    private IActionResult Fail(string message)
        => View("OAuthResult", new OAuthResultVm { Success = false, Message = message });

    private static string GenerateRandomToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool FixedTimeStringEquals(string? a, string? b)
    {
        if (a is null || b is null) return false;
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private sealed record ClaimsValidationResult(
        string? Sub,
        string? GivenName,
        string? FamilyName,
        string? Name,
        string? PreferredUsername,
        string? CompanyName,
        string? CompanyTitle);

    private sealed class TokenExchangeResponse
    {
        public string? id_assertion { get; set; }
        public string? token_type { get; set; }
        public int expires_in { get; set; }
        public string? scope { get; set; }
    }

    private sealed class OAuthErrorResponse
    {
        public string? error { get; set; }
        public string? error_description { get; set; }
    }
}

public sealed class OAuthResultVm
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
