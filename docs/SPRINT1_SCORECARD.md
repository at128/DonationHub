# Sprint 1 — evidence and remaining scope

Verification date: 2026-11-06. This report assesses implementation evidence, not a guaranteed grade. New domain entities, Value Objects and their rules/relationships were explicitly excluded from this correction.

| Criterion | Max | Evidence | Status / remaining scope |
|---|---:|---|---|
| Clean Architecture | 10 | Four primary .NET 10 projects; feature services in Application; thin API mappings; Infrastructure adapters; DI called from Program | PASS for current features. A separate test project is included under tests. |
| Rich Domain Model | 12 | Existing Campaign, Category, RefreshToken and Identity user retained | EXCLUDED: four additional entities, two domain enums, Money/ValueObject and remaining donation invariants were not added. |
| EF Core / migrations | 10 | Separate configurations for existing entities, precision, unique indexes, category and creator Restrict relationships, Campaign RowVersion; initial migration unchanged; additive migrations verified on SQL Server | PARTIAL: mappings/relationships for the excluded domain entities remain absent. |
| Repository / UoW / reads | 6 | IGenericRepository, compatibility IRepository alias, EfRepository/EfUnitOfWork; category and refresh writes; AppUserStore stages Identity account + role for one UoW save; read-only projections | PASS for current workflows. Tests count saves and verify atomic account creation and refresh rollback. |
| Soft delete | 6 | ISoftDeletable.DeletedAtUtc; Campaign property; AuditingInterceptor; global filter; category restriction includes deleted campaigns | PASS. Existing deletion timestamps are backfilled from historical UpdatedAt, not claimed to be exact original deletion times. |
| Options / secrets | 8 | JwtOptions, SeedAdminOptions, DonationOptions; annotations and ValidateOnStart; decoded key validation; legacy AdminSeed compatibility; empty committed connection placeholder | PASS for implemented configuration. Historical scan found no configured credential values or tested credential patterns; this is evidence from a scoped scan, not proof against every possible secret format. |
| Identity | 14 | IDN-01–12 workflows in AuthService; explicit error codes; disabled login/refresh 403; hashed rotating tokens; single-consumer concurrency; development-only demo accounts; Admin staff policy | PASS for tested scenarios. Full token-family revocation is a separate bonus and is not implemented. |
| Categories | 8 | CRUD, bilingual validation, independent uniqueness, DTOs, domain deletion guard, public reads/Admin writes, five development seeds | PASS for tested scenarios. |
| Documentation / delivery | 6 | README setup/secrets/migrations/API checks/design decisions; Scalar/OpenAPI Bearer; original sprint-1 restored at fee5bb6, corrections use sprint-1-fix | PASS after final tag publication and remote verification. Original tag is restored at its original commit, not moved to the fix. |
| Architectural discussion | 20 | Arabic guide covering 23 decisions, examples, alternatives and trade-offs | NOT ASSESSED: the oral review depends on the student's understanding. |

## Executed verification

- `dotnet restore DonationHub.slnx`: succeeded.
- `dotnet build DonationHub.slnx --no-restore`: succeeded, 0 warnings and 0 errors.
- `dotnet test DonationHub.slnx --no-build --no-restore`: 35 passed, 0 failed, 0 skipped. Tests use generated credentials and in-memory SQLite; no production database is required.
- `python tests/verify_api.py`: 64/64 checks passed against fresh SQL Server databases. Covers upgrade from InitialCreate with existing rows, timestamp backfill, rejection of orphan creator references without changing those rows, development/production seeding, seed idempotence, auth, roles, category CRUD, deactivation, three refresh races with rollback checks, a duplicate-registration race, OpenAPI/Scalar, and early startup failure.
- `dotnet ef migrations has-pending-model-changes --no-build --project DonationHub.Infrastructure --startup-project DonationHub.Api`: no pending model changes.
- Local `DonationHubDb`: both additive migrations applied; pre-existing user/category/campaign/refresh-token row counts were unchanged by each migration. Normal development seeding runs separately at application startup.
- InitialCreate migration and its designer remain unchanged.
- Credential audit: 9 pre-correction reachable commits, 75 distinct historical text blobs, configured secret values and current source checked; 0 findings. Credential values were not printed.

The retained SQL verification databases have unique names and are documented by the verifier output. They do not replace or reset DonationHubDb.
