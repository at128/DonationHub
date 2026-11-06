
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
- `DonationHub.Application`: use cases and interfaces, grouped under `Features/Auth` and `Features/Categories`.
- `DonationHub.Infrastructure`: EF Core, SQL Server, Identity, JWT and implementations of persistence interfaces.
- `DonationHub.Api`: Minimal API routes, authorization, HTTP responses and dependency injection composition.

References point inward: Application depends on Domain; Infrastructure depends on Application; Api composes Application and Infrastructure.

## Design decisions

Category and refresh-token writes use `IGenericRepository<T>` and `IUnitOfWork`, with one `SaveChangesAsync` per successful operation. `IRepository<T>` remains as a compatibility alias. Read-only queries use `AsNoTracking`, database-side filtering and DTO projection in Infrastructure, keeping EF Core out of Application. Refresh/logout lookups intentionally track tokens because these operations modify them.

Identity uses its specialized `UserManager`/`AppUserStore` for validation, password hashing and role links. During account creation, automatic saves are deferred and the user remains Added while its role is assigned; one UoW save then commits both records atomically. This preserves Identity's security behavior while meeting the one-save write boundary. Normal updates to existing Identity users retain the framework's default store behavior.

`Category.EnsureCanBeDeleted` owns the deletion rule. Its caller checks for campaigns, including soft-deleted ones, and supplies that fact to the entity. The database foreign key also uses `Restrict` to protect against concurrent changes.

Refresh rotation uses `RevokedAt` as an optimistic concurrency token. Revocation and replacement insertion share one SaveChanges transaction; a second consumer receives `Auth.RefreshTokenInvalid`, and its replacement insert rolls back. This avoids the lock-conversion deadlock risk of the earlier Serializable transaction. Campaign RowVersion remains configured independently.

`TimeProvider` is a Singleton clock; application workflows and the audit interceptor obtain UTC time from it. DbContext, repositories, UoW and feature services are Scoped so one request shares one change tracker. See the [Arabic architectural discussion guide](docs/SPRINT1_DESIGN_DECISIONS.md) for alternatives and trade-offs.

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
dotnet user-secrets set "SeedAdmin:Email" "<your-admin-email>" --project DonationHub.Api
dotnet user-secrets set "SeedAdmin:Password" "<your-admin-password>" --project DonationHub.Api
dotnet user-secrets set "DevelopmentSeed:ManagerEmail" "<your-demo-manager-email>" --project DonationHub.Api
dotnet user-secrets set "DevelopmentSeed:ManagerPassword" "<your-demo-manager-password>" --project DonationHub.Api
dotnet user-secrets set "DevelopmentSeed:DonorEmail" "<your-demo-donor-email>" --project DonationHub.Api
dotnet user-secrets set "DevelopmentSeed:DonorPassword" "<your-demo-donor-password>" --project DonationHub.Api

# Generate a cryptographically random 64-byte key, then store it as Base64.
$jwtBytes = New-Object byte[] 64
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($jwtBytes)
$rng.Dispose()
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($jwtBytes)) --project DonationHub.Api
```

Passwords require at least eight characters, uppercase, lowercase and a digit. `JwtOptions`, `SeedAdminOptions`, `DonationOptions`, database options and development seed options use Data Annotations plus cross-field validation and `ValidateOnStart`. Validation runs before database seeding. The signing key is decoded before its minimum length of 32 bytes is checked. Legacy `AdminSeed:Email` and `AdminSeed:Password` secrets remain supported; `SeedAdmin` takes precedence if both sections exist.

Non-secret defaults are in `appsettings.json`: issuer `DonationHub`, audience `DonationHub.Api`, access tokens 15 minutes and refresh tokens 7 days. Override these with user-secrets or environment variables, for example `Jwt__AccessTokenMinutes`. User-secrets are for local development; use a secret store or environment configuration in deployed environments.

Donation defaults are `MinAmount=1.00`, `MaxAmount=10000.00`, `PendingTimeoutMinutes=30`, and `Currency=USD`. Limits must have at most two decimal places, be positive, and satisfy MinAmount <= MaxAmount. Donation processing itself is outside this change's scope.

Apply the existing migration, then build and run:

```powershell
dotnet ef database update --project DonationHub.Infrastructure --startup-project DonationHub.Api
dotnet build DonationHub.slnx
dotnet run --project DonationHub.Api --launch-profile http
```

Startup seeds the `Admin`, `CampaignManager` and `Donor` roles and the configured Admin. Development additionally seeds one CampaignManager, one Donor, and five bilingual categories (Health, Education, Relief, Orphans, Environment). Demo credentials are required only in Development and must use emails distinct from each other and the Admin. Repeated startup does not duplicate seed data or overwrite existing passwords; an existing seed email with the wrong role causes startup to fail instead of granting additional privileges. Production does not create demo users or categories.

The original migration is unchanged. `AddDeletedAtUtc` adds one nullable Campaign column. Existing deleted campaigns receive their previous UpdatedAt as the best available historical deletion timestamp; new deletions record the exact clock time. Migration commands apply changes without resetting existing data.

`AddCampaignCreatorRelationship` adds the foreign key/index from Campaign.CreatedByUserId to the existing Identity user with Restrict. It checks for orphan references first and fails with a clear message if historical rows need repair; it does not reassign or delete those rows automatically.

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
2. Log in with `email` and `password`, then call `/auth/me` with `Authorization: Bearer <accessToken>`. Wrong email and wrong password both return 401 `Auth.InvalidCredentials`. A disabled account returns 403 `Auth.AccountDeactivated` only after a correct password; it cannot refresh either.
3. Send `{ "refreshToken": "<token>" }` to `/auth/refresh`. It returns a new token pair; invalid, expired, revoked and replayed tokens return 401 `Auth.RefreshTokenInvalid`. Send the new refresh token to `/auth/logout` with a valid access token, then verify it cannot refresh.
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

Run automated tests with `dotnet test DonationHub.slnx`. The test project uses generated test credentials, a controllable clock, and in-memory SQLite for persistence checks; it requires no SQL Server, production database or external service. It covers configuration validation, signed JWT claims, disabled accounts, rotation/replay, UoW save counts, atomic Identity creation, soft deletion, DTO projection and rollback of a racing refresh.

For real SQL Server/API verification on Windows, install Python 3 and run after building:

```powershell
python tests/verify_api.py --server localhost
```

This creates new databases with unique `DonationHub_ApiVerify_` and `DonationHub_ProductionVerify_` names using Windows integrated authentication. It tests migration upgrade with existing data, development/production seeds, auth, concurrent requests, categories, OpenAPI/Scalar and startup failure for invalid settings. Test databases are retained for inspection; existing databases and local secret values are not changed by the script.

Executed results and the remaining excluded rubric items are recorded in the [verification scorecard](docs/SPRINT1_SCORECARD.md).

## Security

- Passwords are managed by ASP.NET Core Identity.
- Access tokens use JWT Bearer authentication.
- Refresh tokens are hashed in the database.
- Refresh tokens are rotated after use.
- Administrative endpoints require the Admin role.
- Secrets must not be committed to the repository.
- Refresh tokens are generated with a cryptographic random number generator and stored as SHA-256 hashes. Rotation revokes the old token and links it to its replacement in one optimistic-concurrency-protected save.
- Campaigns use a global soft-delete query filter and a RowVersion concurrency token. `IgnoreQueryFilters` is used only for the category deletion check so retained campaign references cannot be overlooked.
- SQL Server unique constraints remain the final protection against duplicate names and emails. Category endpoints translate duplicate-name and foreign-key races into 409 responses.

## Current Status

Sprint 1: Identity, Categories, persistence, configuration validation and API documentation. Auth and category endpoints delegate workflows to Application and return DTOs. Errors retain the `error` message and include stable `code` values.

By the requested scope, this change does not add Donation/PaymentAttempt/ProcessedStripeEvent/IdempotencyRecord, additional domain enums, Money/ValueObject, the remaining donation invariants, or their EF mappings/relationships. These rubric items remain incomplete; passing the implemented tests is not a claim of a 100/100 score.

## Submission

The original submission tag `sprint-1` identifies `fee5bb6`; corrections use annotated `sprint-1-fix`. Keep the original checkpoint at its original commit and submit the repository URL with the fix tag during the correction window. Do not move the original tag or force-push it.

Future sprints: Campaigns, Donations, Payments, Notifications,
Reports, and deployment improvements.
