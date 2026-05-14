namespace Micromarin.OAuth.PartnerTest.Models;

public sealed class PartnerOAuthOptions
{
    public string IdentityIssuerBaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CallbackPath { get; set; } = "/oauth/callback";
    public string TokenEndpointPath { get; set; } = "/partner-token";
    public string Scope { get; set; } = "openid profile";
    public string? JwksUri { get; set; }
    public string? DiscoveryUri { get; set; }
    public string? ExpectedAudience { get; set; }
    public int ClockSkewSeconds { get; set; } = 60;
}
