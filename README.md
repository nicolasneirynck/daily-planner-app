# Daily Planner

Daily Planner is a web application designed to help users organize their daily activities and create a clearer overview of their day.

Inspired by planning and organizational challenges often associated with ADHD, the application aims to provide a simple and structured approach to daily planning.

The project is currently under active development.

## Features

The application currently supports:
- Daily planning: View activities organized into six predefined time blocks and navigate between days.
- Activity management: Create, edit, reschedule, and delete planned activities.
- Activity tracking: Mark activities as important or completed.
- Unsaved changes: Receive a warning before discarding unsaved changes.
- User authentication: Register and sign in.


## Getting Started

### Prerequisites

- .NET 10 SDK
- Docker with Docker Compose
- Available ports `5433` (PostgreSQL) and `5001` (application)

### 1. Configure the Environment

From the repository root, create a local environment file:

```bash
cp .env.example .env
```

Update `.env` with your local database credentials. Make sure the PostgreSQL credentials match those in `ConnectionStrings__DatabaseConnection`.

### 2. Start PostgreSQL

```bash
docker compose up -d
```

### 3. Run the Application

```bash
./scripts/run-local.sh
```

Open https://localhost:5001 in your browser.

If necessary, trust the local HTTPS development certificate:

```bash
dotnet dev-certs https --trust
```

### Development Notes

- The startup script loads the environment variables from `.env` and launches the application using the HTTPS development profile.
- In `Development`, EF Core migrations are applied automatically, and sample data and demo accounts are created.
- New accounts currently require an application restart to initialize their six default time blocks.
- Public registration is currently enabled outside `Development`. Review this behavior before deploying to production.


## Development & Testing

### Build

Build the solution from the repository root:

```bash
dotnet build Planning.sln
```

### Automated Tests

Run the domain, service, API, and client tests:

```bash
dotnet test Planning.sln --filter 'FullyQualifiedName!~Planning.E2E.Tests'
```

Docker must be running, as some service and API tests use Testcontainers to create temporary PostgreSQL instances.

### End-to-End Tests

End-to-end browser tests require Google Chrome to be installed.

```bash
dotnet test tests/Planning.E2E.Tests/Planning.E2E.Tests.csproj --settings playwright.chrome.runsettings
```

These tests automatically start a temporary database and web server. The application does not need to be running separately.

## Technical Architecture

### Tech Stack

- **Frontend:** Blazor WebAssembly
- **Backend:** ASP.NET Core (.NET 10), FastEndpoints
- **Database:** PostgreSQL, Entity Framework Core (Npgsql)
- **Authentication:** ASP.NET Core Identity (cookie-based)
- **Logging:** Serilog
- **Testing:** Automated tests, Testcontainers, and browser-based end-to-end tests

### Project Structure

The solution is organized into multiple projects with distinct responsibilities:

| Project | Responsibility |
|---|---|
| `Planning.Client` | Blazor WebAssembly UI and API client |
| `Planning.Server` | ASP.NET Core host, API endpoints, and authentication |
| `Planning.Shared` | Shared API contracts |
| `Planning.Services` | Application logic for daily planning |
| `Planning.Domain` | Domain models and business rules |
| `Planning.Persistence` | EF Core, PostgreSQL configuration, and migrations |

The server hosts both the Blazor client and the API under the same origin. Shared contracts support communication between the client and server, while application logic is separated from HTTP endpoints.

For more details on the architecture and design decisions, see the [architecture documentation](docs/architecture.md).


## API Documentation

The API uses documented response contracts to ensure consistent communication between the client and server.

- [Success Response Contracts](docs/api-success-responses.md)
- [Error Response Contract](docs/api-errors.md)

