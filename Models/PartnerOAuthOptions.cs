namespace Micromarin.OAuth.PartnerTest.Models;

public sealed class PartnerOAuthOptions
{
    public string IdentityIssuerBaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CallbackPath { get; set; } = "/oauth/callback";
    public string Scope { get; set; } = "openid profile";
}
