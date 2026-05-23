# Micromarin OAuth partner test app

Minimal MVC app that acts as an **OpenID Connect relying party**, using the same pattern as [UI.Web.Admin](../Micromarin.UI.Web.Admin/Micromarin.UI.Admin/Startup.cs): `AddOpenIdConnect` + cookie authentication.

## Configuration

Fill in `ClientCredentials` in `appsettings.Development.json` (same section name as Admin):

| Key | Description |
|-----|-------------|
| `Authority` | Identity issuer URL (trailing slash optional) |
| `ClientId` / `ClientSecret` | Partner row in Identity `Client` table |
| `PostLogoutRedirectUri` | Partner app base URL after logout |

**Database:** set `Client.RedirectUri` to `https://{your-host}/signin-oidc` (must match OIDC callback exactly).

## Run

```bash
dotnet run --project Micromarin.OAuth.PartnerTest.csproj
```

1. Open the app and click **Sign in with Micromarin** (`GET /login/micromarin`).
2. ASP.NET Core issues an OIDC **Challenge** → Identity `GET /authorize` (PKCE, authorization code).
3. User signs in on Identity; partner clients see **consent** with profile fields (`GetUserInfo`).
4. Identity redirects to `/signin-oidc?code=...&state=...`.
5. Middleware exchanges the code at `POST /token` (client secret server-side only).
6. User lands on **Profile** with cookie session.

## Render deploy

Environment variables:

- `ClientCredentials__Authority`
- `ClientCredentials__ClientId`
- `ClientCredentials__ClientSecret`
- `ClientCredentials__PostLogoutRedirectUri`

Update the partner client `RedirectUri` in Identity DB to `https://<render-host>/signin-oidc`.

## Notes

- No custom partner APIs (`/login/partner`, `/partner-token`, `id_assertion` callback).
- Demo local login (`Admin` / `Admin`) still uses cookie auth only for UI testing.
