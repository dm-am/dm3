# OpenIddict Implementation for DM3

> **⚠️ ПРИМЕЧАНИЕ:**
> Этот документ является архивным. Актуальная документация по аутентификации перенесена в:
> - **[docs/architecture/AUTHENTICATION.md](./docs/architecture/AUTHENTICATION.md)** - полная документация по аутентификации и авторизации
>
> Данный файл сохранён для исторических целей и может содержать устаревшую информацию.
>
> *Дата переноса: 2026-01-27*

---

## Summary

Successfully implemented OpenIddict authentication backend for DM3 with dual-mode authentication support. The implementation maintains full backward compatibility with the existing X-Dm-Auth-Token header while adding modern OAuth 2.0 support.

## What Was Implemented

### 1. NuGet Packages Added

Added OpenIddict packages to the centralized package management:
- `Directory.Packages.props`: Added OpenIddict.AspNetCore and OpenIddict.EntityFrameworkCore version 5.8.0
- `DM.Web.API.csproj`: References OpenIddict.AspNetCore and OpenIddict.EntityFrameworkCore
- `DM.Services.DataAccess.csproj`: References OpenIddict.EntityFrameworkCore

### 2. Database Configuration

**File: `src/DM.Services.DataAccess/DmDbContext.cs`**
- Added `modelBuilder.UseOpenIddict()` in `OnModelCreating` method
- This configures the EF Core model to include OpenIddict tables

### 3. OpenIddict Configuration in Startup

**File: `src/DM.Web.API/Startup.cs`**

Added comprehensive OpenIddict configuration with:
- **Core**: Entity Framework Core integration with DmDbContext
- **Server**:
  - Password flow enabled
  - Refresh token flow enabled
  - Token endpoint: `/connect/token`
  - Userinfo endpoint: `/connect/userinfo`
  - Anonymous client support
  - Development signing and encryption certificates
  - Endpoint passthrough for custom handling
- **Validation**: Local server validation with ASP.NET Core integration

### 4. Dual-Mode Authentication

Implemented policy scheme that automatically routes requests based on authentication method:
- **Bearer tokens** (Authorization header) → OpenIddict validation
- **X-Dm-Auth-Token header** → Legacy token handler
- Default challenge: OpenIddict

Added ASP.NET Core authentication middleware:
- `UseAuthentication()`
- `UseAuthorization()`

### 5. Legacy Token Authentication Handler

**File: `src/DM.Web.API/Authentication/LegacyTokenAuthenticationHandler.cs`**

Custom authentication handler that:
- Extracts X-Dm-Auth-Token from request headers
- Uses existing `IAuthenticationService.Authenticate(token)` method
- Converts authenticated user to ClaimsPrincipal
- Provides seamless backward compatibility

### 6. OAuth 2.0 Token Endpoint

**File: `src/DM.Web.API/Controllers/v1/Connect/TokenController.cs`**

Implements OAuth 2.0 token endpoint with:
- **Password Grant Flow**:
  - Validates credentials using existing `IAuthenticationService`
  - Creates ClaimsPrincipal with user claims
  - Issues access token and refresh token
- **Refresh Token Flow**:
  - Validates refresh token
  - Checks user still exists and is active
  - Issues new access token
- Error handling with proper OAuth 2.0 error codes
- Rate limiting enabled (5 requests/minute)

Claims issued:
- `sub` (Subject): User ID
- `name`: Username
- `role`: User role
- `username`: Login name
- `session_id`: Session identifier

### 7. OAuth 2.0 Userinfo Endpoint

**File: `src/DM.Web.API/Controllers/v1/Connect/UserinfoController.cs`**

Implements standard OAuth 2.0 userinfo endpoint:
- Returns user claims from authenticated principal
- Supports both GET and POST methods
- Protected with OpenIddict validation
- Returns standard OpenID Connect claims

### 8. Migration Script

**File: `create-openiddict-migration.cmd`**

Helper script to create the OpenIddict database migration. Run this to generate the migration files for OpenIddict tables.

## How to Complete the Setup

### 1. Create Database Migration

Run the migration script:
```cmd
create-openiddict-migration.cmd
```

Or manually:
```bash
cd src\DM.Services.DataAccess
dotnet ef migrations add AddOpenIddict --startup-project ..\DM.Web.API
```

### 2. Apply Migration to Database

```bash
dotnet ef database update --project src\DM.Services.DataAccess --startup-project src\DM.Web.API
```

Or use the `MigrateOnStart` configuration option in appsettings.json.

## Usage

### Legacy Authentication (Backward Compatible)

Continue using X-Dm-Auth-Token header as before:
```http
GET /api/resource
X-Dm-Auth-Token: <encrypted-token>
```

### OAuth 2.0 Password Flow

Request token:
```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=<username>&password=<password>
```

Response:
```json
{
  "access_token": "...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "..."
}
```

Use access token:
```http
GET /api/resource
Authorization: Bearer <access_token>
```

### Refresh Token Flow

Request new token:
```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=refresh_token&refresh_token=<refresh_token>
```

### Get User Info

```http
GET /connect/userinfo
Authorization: Bearer <access_token>
```

Response:
```json
{
  "sub": "user-id",
  "name": "username",
  "username": "login",
  "role": "Player",
  "session_id": "session-id"
}
```

## Architecture Benefits

1. **Backward Compatibility**: Existing clients using X-Dm-Auth-Token continue to work
2. **Standards-Based**: OAuth 2.0 and OpenID Connect compliance
3. **Token Refresh**: Long-lived sessions with refresh tokens
4. **Stateless**: JWT-based access tokens don't require database lookups
5. **Flexible**: Easy to add additional grant types or claims
6. **Secure**: Industry-standard security practices with OpenIddict
7. **Integration Ready**: Standard OAuth 2.0 enables third-party integrations

## Security Considerations

1. **Development Certificates**: The current setup uses development certificates
   - For production, replace with proper certificates:
     ```csharp
     options.AddEncryptionCertificate(certificate);
     options.AddSigningCertificate(certificate);
     ```

2. **Rate Limiting**: Both endpoints have rate limiting enabled (5 requests/minute)

3. **Password Validation**: Continues to use existing password hashing and validation

4. **Token Lifetime**: Configure appropriate token lifetimes in production

5. **HTTPS**: Ensure all endpoints are served over HTTPS in production

## Files Modified

- `Directory.Packages.props` - Added OpenIddict package versions
- `src/DM.Web.API/DM.Web.API.csproj` - Added OpenIddict packages
- `src/DM.Services.DataAccess/DM.Services.DataAccess.csproj` - Added OpenIddict EF package
- `src/DM.Services.DataAccess/DmDbContext.cs` - Added OpenIddict configuration
- `src/DM.Web.API/Startup.cs` - Added OpenIddict and authentication configuration

## Files Created

- `src/DM.Web.API/Authentication/LegacyTokenAuthenticationHandler.cs` - Legacy token handler
- `src/DM.Web.API/Controllers/v1/Connect/TokenController.cs` - OAuth token endpoint
- `src/DM.Web.API/Controllers/v1/Connect/UserinfoController.cs` - OAuth userinfo endpoint
- `create-openiddict-migration.cmd` - Migration helper script

## Build Status

✅ **All code compiles successfully**
- `dotnet restore` completed without errors
- `dotnet build` completed without errors or warnings
- DM.Web.API project builds successfully

## Next Steps

1. Run `create-openiddict-migration.cmd` to create the database migration
2. Apply the migration to your database
3. Test the endpoints with OAuth clients
4. Update frontend to support OAuth 2.0 flows
5. Configure production certificates before deploying
6. Consider adding client credentials grant for service-to-service authentication
7. Add authorization policies as needed

## Testing Recommendations

1. Test legacy authentication continues to work
2. Test password grant flow with valid credentials
3. Test password grant flow with invalid credentials
4. Test refresh token flow
5. Test userinfo endpoint with valid token
6. Test rate limiting behavior
7. Verify tokens contain correct claims
8. Test token expiration and refresh

## Support for Additional Flows

The implementation can be easily extended to support:
- Authorization Code flow (for web applications)
- Client Credentials flow (for service-to-service)
- Implicit flow (legacy SPAs)
- Device Authorization flow (for devices)

Simply enable the desired flows in `Startup.cs` OpenIddict configuration.
