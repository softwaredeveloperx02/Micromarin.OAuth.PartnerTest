# Micromarin Partner SSO — Integration Guide

This document describes how to integrate your application with **Micromarin Identity** as an OpenID Connect (OIDC) relying party. Use the reference implementation in this repository (`Micromarin.OAuth.PartnerTest`) as a starting point.

## Overview

| Item | Value |
|------|-------|
| Protocol | OAuth 2.0 Authorization Code + PKCE |
| Flow | Standard OIDC (`AddOpenIdConnect` in ASP.NET Core) |
| Partner flag | Your client must have `IsPartner = 1` in the Identity `Client` table |
| Callback path | `/signin-oidc` (must match DB `RedirectUri` exactly) |

## Delivery checklist (Micromarin → Partner)

1. **Micromarin** creates a partner `Client` row in the Identity database (see [Database registration](#database-registration)).
2. **Micromarin** shares this repository (or a zip) and this guide.
3. **Micromarin** sends `ClientId` and `ClientSecret` once over a **secure channel** (not email in plain text).
4. **Partner** deploys their app and sends production base URL to Micromarin.
5. **Micromarin** updates `ClientUri` and `RedirectUri` in the database to match the partner host.
6. **Both** run [UAT scenarios](#uat-test-scenarios) before go-live.

## Discovery and endpoints

Given `Authority` = `https://developer-application-identity.azurewebsites.net/`:

| Endpoint | URL |
|----------|-----|
| OpenID configuration | `{Authority}.well-known/openid-configuration` |
| Authorization | `{Authority}authorize` |
| Token | `{Authority}token` |
| UserInfo | `{Authority}api/UserInfo/GetUserInfo` |
| End session (logout) | `{Authority}logout` |

## Application configuration

Configure `ClientCredentials` (see `appsettings.json` or environment variables):

| Key | Description |
|-----|-------------|
| `Authority` | Identity issuer URL |
| `ClientId` | GUID from Micromarin |
| `ClientSecret` | Secret from Micromarin (server-side only) |
| `PostLogoutRedirectUri` | Your app base URL after logout |

Environment variable names (double underscore):

- `ClientCredentials__Authority`
- `ClientCredentials__ClientId`
- `ClientCredentials__ClientSecret`
- `ClientCredentials__PostLogoutRedirectUri`

### OIDC settings (reference)

```csharp
options.UsePkce = true;
options.ResponseType = "code";
options.CallbackPath = "/signin-oidc";
options.Scope.Add("openid");
options.Scope.Add("profile");
options.GetClaimsFromUserInfoEndpoint = true;
options.SaveTokens = true;
```

Sign-in entry point in the reference app: `GET /login/micromarin` → `Challenge(OpenIdConnect)`.

## Database registration

Micromarin registers your client in the Identity `Client` table. Required fields:

| Column | Required value |
|--------|----------------|
| `ClientName` | Your application name |
| `ClientID` | New GUID (shared with you as `ClientId`) |
| `ClientSecret` | New GUID (shared securely once) |
| `ClientUri` | `https://your-app.example.com` (no trailing path) |
| `RedirectUri` | `https://your-app.example.com/signin-oidc` — **exact match** required |
| `GrantTypes` | `code client_credentials` |
| `AllowedScopes` | `openid profile` (add `offline_access` if refresh tokens are needed) |
| `UsePkce` | `1` |
| `IsPartner` | `1` |
| `IsActive` | `1` |
| `BypassMfa` | Per Micromarin policy |

### Example INSERT (Micromarin operations)

Replace placeholders before running:

```sql
DECLARE @ClientId UNIQUEIDENTIFIER = NEWID();
DECLARE @ClientSecret UNIQUEIDENTIFIER = NEWID();

INSERT INTO Client (
    ClientID, ClientSecret, ClientName, ClientUri, RedirectUri,
    IsActive, UsePkce, GrantTypes, AllowedScopes, IsDeleted,
    CreatedTime, CreatedUserID, IsPartner, BypassMfa
)
VALUES (
    @ClientId,
    @ClientSecret,
    N'Your Partner App Name',
    N'https://your-staging.example.com',
    N'https://your-staging.example.com/signin-oidc',
    1, 1,
    N'code client_credentials',
    N'openid profile',
    0,
    GETUTCDATE(),
    N'00000000-0000-0000-0000-000000000000', -- replace with admin user id
    1,
    0
);

SELECT @ClientId AS ClientId, @ClientSecret AS ClientSecret;
```

Store `ClientId` and `ClientSecret` securely; share only with the partner via your secret-handling process.

### Updating RedirectUri after partner deploy

When the partner provides their final URL:

```sql
UPDATE Client
SET ClientUri = N'https://your-production.example.com',
    RedirectUri = N'https://your-production.example.com/signin-oidc',
    ModifiedTime = GETUTCDATE()
WHERE ClientID = 'YOUR-CLIENT-GUID-HERE';
```

The `redirect_uri` sent in `/authorize` must match `RedirectUri` byte-for-byte (scheme, host, port, path). Query strings must match if present.

## Consent and user experience

1. User clicks **Sign in with Micromarin** on your site.
2. Browser redirects to Identity login (`/authorize` with PKCE).
3. User authenticates on Identity.
4. Partner clients see a **consent** screen (`/oauth/consent`) with profile fields.
5. On accept, Identity redirects to `{RedirectUri}?code=...&state=...`.
6. Your app exchanges the code at `POST /token` (with `code_verifier`).
7. Session is established; claims are available from id_token and UserInfo.

If the user denies consent, Identity redirects with `error=access_denied`. The reference app handles this via `OnRemoteFailure` and redirects to `/?denied=1`.

## Claims mapping

Map these claims in your application (see `Services/PartnerOidcClaimMapping.cs`):

| Claim / JSON key | Usage |
|------------------|--------|
| `sub` | Opaque partner-specific user id (not internal Micromarin AccountID) |
| `given_name` | First name |
| `family_name` | Last name |
| `preferred_username` | Username |
| `email` | Email |
| `company_name` | Company |
| `company_title` | Job title |

ASP.NET Core does not map custom claims by default; copy the reference `OnUserInformationReceived` / `OnTokenValidated` handlers.

## Security notes

- Never commit `ClientSecret` to source control.
- Use HTTPS in production for your app and callback URL.
- PKCE is required when `UsePkce = 1` on the client record.
- Do not use custom legacy partner endpoints (`/login/partner`, signed redirects, etc.); use standard OIDC only.

## Reference app — demo login (development only)

The sample form login (`Admin` / `Admin`) is for **local UI testing only**. It does not call Micromarin Identity. Disable or remove it before production deployment.

## UAT test scenarios

Run these before go-live:

| # | Scenario | Expected result |
|---|----------|-----------------|
| 1 | `RedirectUri` in DB matches `{your-host}/signin-oidc` | Authorize shows login, not Identity error page |
| 2 | Sign in with Micromarin → accept consent | Redirect to `/signin-oidc`, then profile with claims populated |
| 3 | Sign in → deny consent | Redirect to partner with `access_denied` (reference: `/?denied=1`) |
| 4 | Sign out | Partner and Identity sessions cleared; land on `PostLogoutRedirectUri` |
| 5 | Wrong `redirect_uri` in authorize request | Identity returns `invalid_request` (partner clients only) |
| 6 | Token exchange with valid `code_verifier` | `access_token` and `id_token` (if `openid` scope) returned |
| 7 | UserInfo endpoint | `company_name`, `given_name`, etc. present |

## Troubleshooting

| Symptom | Likely cause |
|---------|----------------|
| Identity `/Home/Error?error=invalid_request` | `redirect_uri` mismatch vs DB `RedirectUri` (partner clients) |
| `invalid_scope` | Requested scope not in `AllowedScopes` |
| `code challenge required` | PKCE not sent; enable `UsePkce` in client config and middleware |
| Empty profile claims | Missing UserInfo mapping or `GetClaimsFromUserInfoEndpoint = false` |

## Support

Contact your Micromarin integration contact for:

- New partner client registration
- `RedirectUri` updates for new environments
- Credential rotation

## Related files in this repository

| File | Purpose |
|------|---------|
| `Program.cs` | OIDC middleware configuration |
| `Services/PartnerOidcClaimMapping.cs` | Claim mapping from id_token / UserInfo |
| `Controllers/HomeController.cs` | Login challenge and profile |
| `appsettings.json` | Configuration template |
