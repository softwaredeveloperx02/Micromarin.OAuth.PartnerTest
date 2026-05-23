using System.Security.Claims;
using Micromarin.OAuth.PartnerTest.Models;
using Micromarin.OAuth.PartnerTest.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ClientCredentialsOptions>(
    builder.Configuration.GetSection("ClientCredentials"));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var clientCredentials = builder.Configuration.GetSection("ClientCredentials").Get<ClientCredentialsOptions>()
    ?? new ClientCredentialsOptions();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "PartnerApp.Auth";
    options.Cookie.HttpOnly = true;
    options.LoginPath = "/";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.RequireHttpsMetadata = true;
    options.UsePkce = true;

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");

    options.Authority = clientCredentials.Authority;
    options.ClientId = clientCredentials.ClientId;
    options.ClientSecret = clientCredentials.ClientSecret;

    options.ResponseType = "code";
    options.CallbackPath = "/signin-oidc";

    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;

    options.NonceCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;

    options.Events.OnUserInformationReceived = context =>
    {
        PartnerOidcClaimMapping.ApplyUserInfoDocument(
            context.Principal?.Identity as ClaimsIdentity,
            context.User);
        return Task.CompletedTask;
    };

    options.Events.OnTokenValidated = context =>
    {
        if (context.Principal?.Identity is ClaimsIdentity identity &&
            context.SecurityToken is JwtSecurityToken jwt)
        {
            PartnerOidcClaimMapping.ApplyIdTokenClaims(identity, jwt.Claims);
        }

        context.Properties!.IsPersistent = true;
        context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToIdentityProviderForSignOut = context =>
    {
        if (!string.IsNullOrWhiteSpace(clientCredentials.PostLogoutRedirectUri))
        {
            context.ProtocolMessage.PostLogoutRedirectUri = clientCredentials.PostLogoutRedirectUri;
        }
        return Task.CompletedTask;
    };

    options.Events.OnRemoteFailure = context =>
    {
        var failureMessage = context.Failure?.Message ?? string.Empty;
        var redirect = failureMessage.Contains("access_denied", StringComparison.OrdinalIgnoreCase)
            ? "/?denied=1"
            : "/?ssoError=1";

        context.Response.Redirect(redirect);
        context.HandleResponse();
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
