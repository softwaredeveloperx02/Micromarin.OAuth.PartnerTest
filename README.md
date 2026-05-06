# Micromarin OAuth partner test app

Minimal MVC app **outside** the `Micromarin.UI.Web.Identity` repo. It exercises Micromarin partner SSO login against Identity.

## Configuration

1. Fill in the `PartnerOAuth` section in `appsettings.Development.json`:
   - `IdentityIssuerBaseUrl`: Root URL of your Identity instance (same idea as `OAuthOptions:IDPUri`, e.g. `https://localhost:5xxxx/`).
   - `ClientId` / `ClientSecret`: Row in the Identity `Client` table.
   - `CallbackPath`: Callback path used in this app (default `/oauth/callback`). The full URL (`scheme://host/oauth/callback`) must match **`Client.RedirectUri`** in Identity **exactly** (scheme, host, path).
   - `Scope`: Scopes requested for consent screen display (for example `openid profile`).

2. Run Partner on another port/host than Identity when needed; add origins to **`OAuthOptions:ClientUrls`** in Identity if your scenario requires cookie/CORS rules.

## Run

```bash
dotnet run --project Micromarin.OAuth.PartnerTest.csproj
```

The **Sign in with Micromarin** button calls `/login/micromarin`, which redirects to Identity `/login` with partner parameters.  
After Micromarin authentication and consent, Identity redirects back to `/oauth/callback` with signed query values.  
The callback verifies signature + timestamp and then shows success/failure.

## Quick demo deploy (Render, free)

1. Push this project to GitHub.
2. In Render, create a **new Web Service** from that repo.
3. Render will auto-detect `render.yaml` and Docker runtime.
4. Set required environment values in Render:
   - `PartnerOAuth__IdentityIssuerBaseUrl`
   - `PartnerOAuth__ClientId`
   - `PartnerOAuth__ClientSecret`
5. Deploy and copy the public URL (for example `https://micromarin-oauth-partner-ui.onrender.com`).
6. In Identity DB, update the partner client `RedirectUri` to:
   - `https://<your-render-domain>/oauth/callback`
   - Must match exactly.

## Notes

- This project is for **development/testing** only; do not use production secrets here.
