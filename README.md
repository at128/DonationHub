
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

- DonationHub.Domain
- DonationHub.Application
- DonationHub.Infrastructure
- DonationHub.Api

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

1. Clone the repository.
2. Configure `ConnectionStrings:DefaultConnection`.
3. Configure JWT settings and AdminSeed credentials
   using .NET User Secrets.
4. Apply database migrations:

```powershell
dotnet ef database update --project DonationHub.Infrastructure --startup-project DonationHub.Api
```

5. Run the application:

```powershell
dotnet run --project DonationHub.Api
```

## API Documentation

Development only:

- http://localhost:5153/scalar
- http://localhost:5153/openapi/v1.json

## Security

- Passwords are managed by ASP.NET Core Identity.
- Access tokens use JWT Bearer authentication.
- Refresh tokens are hashed in the database.
- Refresh tokens are rotated after use.
- Administrative endpoints require the Admin role.
- Secrets must not be committed to the repository.

## Current Status

Sprint 1: Identity, Categories, persistence, and API documentation.

Future sprints: Campaigns, Donations, Payments, Notifications,
Reports, and deployment improvements.
