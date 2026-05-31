# Micromarin OAuth partner reference app

Minimal MVC app that acts as an **OpenID Connect relying party**, using the same pattern as Micromarin Admin/Cloud: `AddOpenIdConnect` + cookie authentication.

**For partners:** read the full integration guide in [PARTNER_INTEGRATION.md](./PARTNER_INTEGRATION.md) (endpoints, database registration, UAT, troubleshooting).

## Quick start

Fill in `ClientCredentials` in `appsettings.Development.json`:

| Key | Description |
|-----|-------------|
| `Authority` | Identity issuer URL (trailing slash optional) |
| `ClientId` / `ClientSecret` | Partner row in Identity `Client` table (`IsPartner = 1`) |
| `PostLogoutRedirectUri` | Partner app base URL after logout |

**Database (Micromarin):** set `Client.RedirectUri` to `https://{your-host}/signin-oidc` (must match OIDC callback exactly).

```bash
dotnet run --project Micromarin.OAuth.PartnerTest.csproj
```

1. Open the app and click **Login with Micromarin** (`GET /login/micromarin`).
2. OIDC **Challenge** → Identity `GET /authorize` (PKCE, authorization code).
3. User signs in on Identity; partner clients see **consent** with profile fields.
4. Identity redirects to `/signin-oidc?code=...&state=...`.
5. Middleware exchanges the code at `POST /token`.
6. User lands on **Profile** with cookie session.

## Demo login (development only)

The form login (`Admin` / `Admin`) is for **local UI testing only**. It does not use Micromarin Identity. **Do not enable in production.**

## Deploy (e.g. Render)

Environment variables:

- `ClientCredentials__Authority`
- `ClientCredentials__ClientId`
- `ClientCredentials__ClientSecret`
- `ClientCredentials__PostLogoutRedirectUri`

Ask Micromarin to set the partner client `RedirectUri` to `https://<your-host>/signin-oidc`.

## Notes

- Standard OIDC only — no custom partner APIs.
- All user-facing strings in this reference app are English.
