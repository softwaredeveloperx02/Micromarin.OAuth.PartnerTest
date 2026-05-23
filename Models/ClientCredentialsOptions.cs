namespace Micromarin.OAuth.PartnerTest.Models;

/// <summary>
/// OIDC confidential client settings (same shape as UI.Web.Admin appsettings ClientCredentials).
/// </summary>
public sealed class ClientCredentialsOptions
{
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string PostLogoutRedirectUri { get; set; } = "";
}
