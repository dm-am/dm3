# OpenIddict Backend Implementation - Quick Start Guide

> **⚠️ ПРИМЕЧАНИЕ:**
> Этот документ является архивным. Актуальная документация по аутентификации перенесена в:
> - **[docs/architecture/AUTHENTICATION.md](./docs/architecture/AUTHENTICATION.md)** - полная документация по аутентификации и авторизации
>
> Данный файл сохранён для исторических целей и может содержать устаревшую информацию.
>
> *Дата переноса: 2026-01-27*

---

## ✅ Implementation Complete

All code has been written and verified to compile successfully!

## What's Included

1. ✅ OpenIddict NuGet packages added
2. ✅ Database context configured for OpenIddict
3. ✅ Startup.cs configured with OpenIddict Core, Server, and Validation
4. ✅ Dual-mode authentication (Bearer + Legacy X-Dm-Auth-Token)
5. ✅ LegacyTokenAuthenticationHandler for backward compatibility
6. ✅ TokenController with password and refresh_token grant types
7. ✅ UserinfoController for OAuth 2.0 userinfo endpoint
8. ✅ All code compiles without errors

## Quick Commands

### 1. Create the Database Migration

```bash
cd src/DM.Services.DataAccess
dotnet ef migrations add AddOpenIddict --startup-project ../DM.Web.API
cd ../..
```

Or use the provided script:
```cmd
create-openiddict-migration.cmd
```

### 2. Apply Migration to Database

```bash
dotnet ef database update --project src/DM.Services.DataAccess --startup-project src/DM.Web.API
```

Or set `MigrateOnStart: true` in appsettings.json and run the app once.

### 3. Build and Run

```bash
dotnet build
dotnet run --project src/DM.Web.API
```

## Test the Endpoints

### Get OAuth Token (Password Flow)

```bash
curl -X POST http://localhost:5000/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=youruser&password=yourpass"
```

Response:
```json
{
  "access_token": "eyJhbGc...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "..."
}
```

### Use the Access Token

```bash
curl -X GET http://localhost:5000/api/some-endpoint \
  -H "Authorization: Bearer eyJhbGc..."
```

### Refresh Token

```bash
curl -X POST http://localhost:5000/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=refresh_token&refresh_token=YOUR_REFRESH_TOKEN"
```

### Get User Info

```bash
curl -X GET http://localhost:5000/connect/userinfo \
  -H "Authorization: Bearer eyJhbGc..."
```

### Legacy Authentication (Still Works!)

```bash
curl -X GET http://localhost:5000/api/some-endpoint \
  -H "X-Dm-Auth-Token: your-legacy-token"
```

## Key Features

### 🔄 Backward Compatible
- Existing clients using X-Dm-Auth-Token continue to work unchanged
- No breaking changes to existing authentication

### 🔐 OAuth 2.0 Standard
- Password grant for username/password authentication
- Refresh token grant for token renewal
- Userinfo endpoint for user claims

### 🎯 Dual-Mode Authentication
- Automatically detects Bearer tokens (OAuth) vs X-Dm-Auth-Token (Legacy)
- No client changes needed to support both modes

### 🛡️ Secure by Design
- Uses existing password validation and hashing
- Rate limiting on authentication endpoints (5 requests/minute)
- JWT-based access tokens

### 📊 Claims Issued
- `sub` - User ID
- `name` - Username
- `role` - User role
- `username` - Login name
- `session_id` - Session ID

## Configuration

All OpenIddict configuration is in `src/DM.Web.API/Startup.cs`:

- Token endpoint: `/connect/token`
- Userinfo endpoint: `/connect/userinfo`
- Anonymous clients allowed (no client authentication required)
- Development certificates (replace for production!)

## For Production Deployment

### 1. Replace Development Certificates

In `Startup.cs`, replace:
```csharp
options.AddDevelopmentEncryptionCertificate()
    .AddDevelopmentSigningCertificate();
```

With:
```csharp
options.AddEncryptionCertificate(yourCertificate)
    .AddSigningCertificate(yourCertificate);
```

### 2. Configure Token Lifetimes

Add to OpenIddict server configuration:
```csharp
options.SetAccessTokenLifetime(TimeSpan.FromHours(1));
options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
```

### 3. Enable HTTPS Only

Ensure all endpoints are served over HTTPS in production.

### 4. Update CORS Settings

Update `appsettings.json` to allow your frontend domain:
```json
{
  "IntegrationSettings": {
    "CorsUrls": ["https://your-frontend.com"]
  }
}
```

## Troubleshooting

### Migration Issues

If you get errors creating the migration:
1. Ensure PostgreSQL is running
2. Check connection string in appsettings.json
3. Verify all packages are restored: `dotnet restore`

### Build Errors

The implementation has been verified to compile. If you see errors:
1. Run `dotnet restore`
2. Run `dotnet clean`
3. Run `dotnet build`

### Authentication Not Working

1. Check database migration was applied
2. Verify user exists and is activated
3. Check credentials are correct
4. Verify rate limiting hasn't blocked requests

## Architecture Overview

```
┌─────────────────┐
│   Client App    │
└────────┬────────┘
         │
         ├─────────────┐
         │             │
    Bearer Token   X-Dm-Auth-Token
         │             │
         │             │
    ┌────▼─────────────▼────┐
    │  Policy Scheme Router  │
    └────┬─────────────┬────┘
         │             │
         │             │
    ┌────▼──────┐ ┌───▼────────────────┐
    │ OpenIddict│ │ Legacy Token       │
    │ Validation│ │ Authentication     │
    └────┬──────┘ └───┬────────────────┘
         │             │
         └──────┬──────┘
                │
         ┌──────▼──────────────┐
         │ IAuthenticationService│
         │  (Existing Service)  │
         └─────────────────────┘
```

## Files Reference

### Modified Files
- `Directory.Packages.props`
- `src/DM.Web.API/DM.Web.API.csproj`
- `src/DM.Services.DataAccess/DM.Services.DataAccess.csproj`
- `src/DM.Services.DataAccess/DmDbContext.cs`
- `src/DM.Web.API/Startup.cs`

### New Files
- `src/DM.Web.API/Authentication/LegacyTokenAuthenticationHandler.cs`
- `src/DM.Web.API/Controllers/v1/Connect/TokenController.cs`
- `src/DM.Web.API/Controllers/v1/Connect/UserinfoController.cs`

## Next Steps

1. ✅ Code is complete and compiles
2. ⏭️ Create database migration
3. ⏭️ Apply migration
4. ⏭️ Test endpoints
5. ⏭️ Update frontend to use OAuth
6. ⏭️ Configure production certificates
7. ⏭️ Deploy!

## Need Help?

See `OPENIDDICT_IMPLEMENTATION.md` for detailed implementation notes and architecture documentation.

---

**Status**: ✅ Ready for migration creation and testing!
