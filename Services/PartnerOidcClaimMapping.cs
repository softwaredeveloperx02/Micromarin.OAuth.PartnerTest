using System.Security.Claims;
using System.Text.Json;

namespace Micromarin.OAuth.PartnerTest.Services;

/// <summary>
/// Maps Identity userinfo / id_token JSON into cookie claims for the profile card.
/// ASP.NET Core OIDC does not map custom keys (company_name, etc.) by default.
/// </summary>
public static class PartnerOidcClaimMapping
{
    private static readonly (string JsonKey, string ClaimType)[] ProfileClaimMap =
    {
        ("sub", ClaimTypes.NameIdentifier),
        ("given_name", ClaimTypes.GivenName),
        ("family_name", ClaimTypes.Surname),
        ("preferred_username", "preferred_username"),
        ("email", ClaimTypes.Email),
        ("company_name", "company_name"),
        ("company_title", "company_title"),
    };

    public static void ApplyUserInfoDocument(ClaimsIdentity? identity, JsonDocument userInfo)
    {
        if (identity == null)
            return;

        var root = userInfo.RootElement;
        foreach (var (jsonKey, claimType) in ProfileClaimMap)
            UpsertFromJson(identity, root, jsonKey, claimType);
    }

    public static void ApplyIdTokenClaims(ClaimsIdentity? identity, IEnumerable<Claim> tokenClaims)
    {
        if (identity == null)
            return;

        foreach (var (jsonKey, claimType) in ProfileClaimMap)
        {
            var value = tokenClaims.FirstOrDefault(c =>
                string.Equals(c.Type, jsonKey, StringComparison.OrdinalIgnoreCase))?.Value;

            if (!string.IsNullOrWhiteSpace(value))
                Upsert(identity, claimType, value);
        }
    }

    private static void UpsertFromJson(ClaimsIdentity identity, JsonElement root, string jsonKey, string claimType)
    {
        if (!root.TryGetProperty(jsonKey, out var prop) || prop.ValueKind != JsonValueKind.String)
            return;

        var value = prop.GetString();
        if (!string.IsNullOrWhiteSpace(value))
            Upsert(identity, claimType, value);
    }

    private static void Upsert(ClaimsIdentity identity, string claimType, string value)
    {
        var existing = identity.FindAll(claimType).ToList();
        foreach (var claim in existing)
            identity.RemoveClaim(claim);

        identity.AddClaim(new Claim(claimType, value));
    }
}
