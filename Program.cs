using Micromarin.OAuth.PartnerTest.Models;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PartnerOAuthOptions>(builder.Configuration.GetSection("PartnerOAuth"));
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(sp =>
{
    var oauth = builder.Configuration.GetSection("PartnerOAuth").Get<PartnerOAuthOptions>()
                ?? new PartnerOAuthOptions();

    var discoveryUri = !string.IsNullOrWhiteSpace(oauth.DiscoveryUri)
        ? oauth.DiscoveryUri
        : oauth.IdentityIssuerBaseUrl.TrimEnd('/') + "/.well-known/openid-configuration";

    var retriever = new OpenIdConnectConfigurationRetriever();
    var http = new HttpDocumentRetriever { RequireHttps = false };
    return new ConfigurationManager<OpenIdConnectConfiguration>(discoveryUri, retriever, http)
    {
        AutomaticRefreshInterval = TimeSpan.FromHours(24),
        RefreshInterval = TimeSpan.FromMinutes(5)
    };
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpClient("identity", c =>
{
    c.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

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
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
