
# DonationHub

DonationHub is a charity donation management REST API
built with ASP.NET Core 10 and Clean Architecture.

## Tech Stack

- .NET 10 / ASP.NET Core Minimal APIs
- Entity Framework Core 10
- SQL Server
- ASP.NET Core Identity
- JWT Authentication and Refresh Token Rotation
- OpenAPI and Scalar

## Architecture

- `DonationHub.Domain`: entities, validation and business rules, without external packages.
- `DonationHub.Application`: use cases and interfaces. The category workflow and its DTOs live in `Features/Categories`.
- `DonationHub.Infrastructure`: EF Core, SQL Server, Identity, JWT and implementations of persistence interfaces.
- `DonationHub.Api`: Minimal API routes, authorization, HTTP responses and dependency injection composition.

References point inward: Application depends on Domain; Infrastructure depends on Application; Api composes Application and Infrastructure.

Category writes use the existing `IRepository<T>` and `IUnitOfWork`, with one `SaveChangesAsync` per successful write. Reads use `ICategoryQueries`, implemented in Infrastructure with `AsNoTracking`, database-side filtering and projection directly into DTOs. This avoids loading tracked entities for read-only responses while keeping EF Core out of Application.

`Category.EnsureCanBeDeleted` owns the deletion rule. Its caller checks for campaigns, including soft-deleted ones, and supplies that fact to the entity. The database foreign key also uses `Restrict` to protect against concurrent changes.

## Sprint 1 Features

### Identity
- Donor registration
- Login with JWT
- Refresh token rotation
- Logout and token revocation
- Current user profile
- Admin staff creation
- Role-based authorization

### Categories
- Public category listing and details
- Admin category creation
- Admin category updates
- Admin category deletion
- Unique Arabic and English names

## Local Setup

Requirements:
- .NET 10 SDK
- SQL Server
- EF Core CLI 10 (`dotnet tool install --global dotnet-ef --version 10.0.14`, if not installed)

Run the following from the repository root. The Api project already has a UserSecretsId. Replace the placeholders with your local values; do not put credentials in appsettings files.

```powershell
dotnet restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=DonationHubDb;Trusted_Connection=True;TrustServerCertificate=True;" --project DonationHub.Api
dotnet user-secrets set "AdminSeed:Email" "<your-admin-email>" --project DonationHub.Api
dotnet user-secrets set "AdminSeed:Password" "<your-admin-password>" --project DonationHub.Api

# Generate a cryptographically random 64-byte key, then store it as Base64.
$jwtBytes = New-Object byte[] 64
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($jwtBytes)
$rng.Dispose()
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($jwtBytes)) --project DonationHub.Api
```

Passwords require at least eight characters, uppercase, lowercase and a digit. Startup validates that the JWT issuer, audience, key and positive token lifetimes are configured, and that AdminSeed email and password are supplied. The signing key is decoded before its minimum length of 32 bytes is checked.

Non-secret defaults are in `appsettings.json`: issuer `DonationHub`, audience `DonationHub.Api`, access tokens 15 minutes and refresh tokens 7 days. Override these with user-secrets or environment variables, for example `Jwt__AccessTokenMinutes`. User-secrets are for local development; use a secret store or environment configuration in deployed environments.

Apply the existing migration, then build and run:

```powershell
dotnet ef database update --project DonationHub.Infrastructure --startup-project DonationHub.Api
dotnet build DonationHub.slnx
dotnet run --project DonationHub.Api --launch-profile http
```

Startup seeds the `Admin`, `CampaignManager` and `Donor` roles and the configured Admin. Repeated startup does not duplicate them. Category records are created through the API; demo users and categories are not automatically seeded in this version.

## API Documentation

Development only:

- http://localhost:5153/scalar
- http://localhost:5153/openapi/v1.json

In Scalar, log in, copy `accessToken`, then enter it in the Bearer authentication field. Protected operations declare the Bearer security requirement. Do not paste a refresh token into the Bearer field.

## API verification

All routes start with `/api/v1`.

| Method | Route | Access |
|---|---|---|
| POST | `/auth/register` | Public |
| POST | `/auth/login` | Public |
| POST | `/auth/refresh` | Public |
| POST | `/auth/logout` | Authenticated |
| GET | `/auth/me` | Authenticated |
| POST | `/admin/staff` | Admin |
| GET | `/categories`, `/categories/{id}` | Public |
| POST | `/categories` | Admin |
| PUT, DELETE | `/categories/{id}` | Admin |

Use Scalar or an HTTP client to check these flows:

1. Register with `email`, `password`, `fullName` and `preferredLanguage` (`ar` or `en`). The account receives the Donor role. Duplicate email is rejected regardless of case.
2. Log in with `email` and `password`, then call `/auth/me` with `Authorization: Bearer <accessToken>`. Wrong email and wrong password both return the same generic 401 response.
3. Send `{ "refreshToken": "<token>" }` to `/auth/refresh`. It returns a new token pair; reusing the old refresh token returns 401. Send the new refresh token to `/auth/logout` with a valid access token, then verify it cannot refresh.
4. Log in as the seeded Admin. Create staff with `email`, `temporaryPassword`, `fullName`, `preferredLanguage` and `role` (`Admin` or `CampaignManager`). Anonymous calls return 401 and Donor calls return 403.
5. Create a category with `nameAr`, `nameEn`, `descriptionAr` and `descriptionEn`. Both names are required, limited to 100 characters and independently unique. Read, update and delete it. Check duplicate names (409), invalid names (400), unknown IDs (404), and Admin-only mutations (401/403).
6. If a category is linked to an existing campaign, deleting it returns 409 with `Category.HasCampaigns`, including when that campaign is soft-deleted. Unknown category IDs return `Category.NotFound`.

```json
{
  "nameAr": "الصحة",
  "nameEn": "Health",
  "descriptionAr": "دعم الرعاية الصحية",
  "descriptionEn": "Support healthcare"
}
```

`dotnet test DonationHub.slnx` can be run, but this version does not contain an automated test project. A successful exit alone does not mean API scenarios were tested. Use a separate development database for manual checks.

## Security

- Passwords are managed by ASP.NET Core Identity.
- Access tokens use JWT Bearer authentication.
- Refresh tokens are hashed in the database.
- Refresh tokens are rotated after use.
- Administrative endpoints require the Admin role.
- Secrets must not be committed to the repository.
- Refresh tokens are generated with a cryptographic random number generator and stored as SHA-256 hashes. Rotation revokes the old token and links it to its replacement inside a transaction.
- Campaigns use a global soft-delete query filter and a RowVersion concurrency token. `IgnoreQueryFilters` is used only for the category deletion check so retained campaign references cannot be overlooked.
- SQL Server unique constraints remain the final protection against duplicate names and emails. Category endpoints translate duplicate-name and foreign-key races into 409 responses.

## Current Status

Sprint 1: Identity, Categories, persistence, and API documentation. This cleanup moves category orchestration into Application and shares account-creation logic between registration and staff creation. It preserves the existing database schema and route/DTO contracts; category errors now include a stable `code` alongside the existing `error` message.

Future sprints: Campaigns, Donations, Payments, Notifications,
Reports, and deployment improvements.
