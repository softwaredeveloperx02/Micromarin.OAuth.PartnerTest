using System.Security.Claims;

namespace Micromarin.OAuth.PartnerTest.Models;

public sealed class UserProfileVm
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string CompanyTitle { get; set; } = "";
    public string LoginSource { get; set; } = "";
    public string? Email { get; set; }
    public string? PartnerSub { get; set; }

    public string Initials
    {
        get
        {
            var f = string.IsNullOrWhiteSpace(FirstName) ? "" : FirstName.Trim()[..1];
            var l = string.IsNullOrWhiteSpace(LastName) ? "" : LastName.Trim()[..1];
            var combined = (f + l).ToUpperInvariant();
            return string.IsNullOrEmpty(combined) ? "U" : combined;
        }
    }

    public string FullName => string.Join(" ", new[] { FirstName, LastName }
        .Where(s => !string.IsNullOrWhiteSpace(s)));

    public static UserProfileVm FromClaims(ClaimsPrincipal user, string loginSource = "Micromarin SSO")
    {
        var email = ClaimValue(user, ClaimTypes.Email, "email");
        var firstName = ClaimValue(user, ClaimTypes.GivenName, "given_name");
        var lastName = ClaimValue(user, ClaimTypes.Surname, "family_name");
        var userName = ClaimValue(user, "preferred_username", "preferred_username");

        // Do not treat email as first name when given_name is missing.
        if (string.IsNullOrWhiteSpace(firstName) || string.Equals(firstName, email, StringComparison.OrdinalIgnoreCase))
            firstName = null;

        return new UserProfileVm
        {
            FirstName = firstName ?? "",
            LastName = lastName ?? "",
            UserName = userName ?? "",
            CompanyName = ClaimValue(user, "company_name", "company_name") ?? "",
            CompanyTitle = ClaimValue(user, "company_title", "company_title") ?? "",
            Email = email,
            PartnerSub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst("sub")?.Value,
            LoginSource = user.FindFirst("login_source")?.Value ?? loginSource,
        };
    }

    private static string? ClaimValue(ClaimsPrincipal user, string type1, string type2)
    {
        var v = user.FindFirst(type1)?.Value ?? user.FindFirst(type2)?.Value;
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
