# Planning

Planning is a .NET 10 application based on a starter solution. It has an ASP.NET Core API, a hosted Blazor WebAssembly client, PostgreSQL persistence, authentication, and example features.

## Requirements

- .NET 10 SDK
- Docker with Docker Compose, for the local PostgreSQL database
- Optional: `dotnet-ef` for creating migrations (`dotnet tool install --global dotnet-ef`)


## Run locally

From the repository root:

```bash
cp .env.example .env
# Set the same database, user, and password in the POSTGRES_* values and connection string.
docker compose up -d
./scripts/run-local.sh
```

If `.env` already exists, keep it and skip the copy command. The start script exports its values as environment variables before running the server.

Open `https://localhost:5001`. The server hosts the Blazor client and API at the same origin. The server launch profile sets `ASPNETCORE_ENVIRONMENT=Development`. If the development certificate is not trusted, run `dotnet dev-certs https --trust`.

The local database is exposed on host port `5433` (container port `5432`). `.env.example` uses database `adhd-planning` and user `devusr`; choose your own local password. Docker Compose reads the `POSTGRES_*` values from `.env`. The start script exports `ConnectionStrings__DatabaseConnection`; .NET maps the double underscore to `ConnectionStrings:DatabaseConnection`. This environment value takes precedence over the optional, ignored `src/Planning.Server/appsettings.Development.json`. For deployment, provide the connection string through environment configuration or a secret store; no production connection string is included.

In Development, the server applies EF Core migrations and seeds example data on startup. This requires PostgreSQL to be running. In other environments, apply migrations as part of deployment before starting the server.

To confirm that the initial migration was applied to the local database:

```bash
docker compose exec -T db sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"'
```

The result should include `20261004181924_Initial`.

## Build and test

```bash
dotnet build Planning.sln
dotnet test Planning.sln --filter 'FullyQualifiedName!~Planning.E2E.Tests'
```

The E2E project is included in the solution so Rider can discover it. Start Docker, then use the **E2E Tests** Rider configuration or run `dotnet test tests/Planning.E2E.Tests/Planning.E2E.Tests.csproj --settings playwright.chrome.runsettings`. The tests use the installed Google Chrome browser and start their own temporary PostgreSQL database and app server with a test account; the local app and database do not need to be running.

To add a migration from the repository root:

```bash
dotnet ef migrations add YourMigrationName --startup-project src/Planning.Server --project src/Planning.Persistence
```

## Included examples

- **Products:** domain entity, EF mapping, service, shared contracts and validation, API endpoints, Blazor pages, and tests. Listing is public; creating a product requires the Administrator role.
- **Projects:** domain entities and an address value object, EF mapping, a service and endpoint for editing, and service tests. A Technician can edit a project assigned to their account. The endpoint also accepts the Administrator role, but the service still checks project ownership.
- **Identity:** registration, login, logout, account information, role endpoints, cookie authentication, and protected example pages. Registration is currently public; new accounts receive the User role. Review access rules for the finished application.

Development seeding creates `admin@example.com`, `user@example.com`, `technician1@example.com`, and `technician2@example.com` with password `A1b2C3!`. The seeded roles are Administrator, Technician, and User; `user@example.com` receives the User role. These accounts and the password are for local development only. Replace or disable this seed data before using real accounts.

## Structure

| Project | Responsibility |
| --- | --- |
| `Planning.Domain` | Entities, value objects, and domain rules |
| `Planning.Shared` | Client/server contracts and validators |
| `Planning.Persistence` | EF Core context, mappings, migrations, triggers, and development seeding |
| `Planning.Services` | Application use cases |
| `Planning.Server` | FastEndpoints API, authentication, logging, and client hosting |
| `Planning.Client` | Blazor WebAssembly UI |
| `Planning.*.Tests` | Domain, service, and Blazor component tests |

FastEndpoints results are sent through a central response processor. Serilog handles structured logging. The tests cover selected example behavior; the solution does not include database or API integration tests.

## Architecture and design

The solution combines a layered structure with feature-oriented code. Domain rules live in `Domain`; the `Products`, `Projects`, and `Identity` folders group related use cases within the other projects. `Server` is the composition root: it registers the application services and connects the API, persistence, and hosted client.

### Domain

`Domain` contains entities and value objects without a dependency on the UI or database provider. `Product` is a simple entity example. `Project` shows a relationship to `Technician`, an owned `Address` value object, and an ownership rule used when editing. `Entity` and `ValueObject` provide reusable base behavior. This keeps business rules close to the data they govern.

### Shared contracts

`Shared` defines the requests, responses, DTOs, service interfaces, and FluentValidation validators used across the client and server. The client communicates through these contracts instead of depending on domain entities or EF Core types. Changing an API contract can therefore affect both sides and should be treated deliberately.

### Services

`Services` implements application use cases. It queries or updates the EF Core context and returns `Ardalis.Result` values. `ProductService` demonstrates creation, searching, ordering, and paging. `ProjectService` uses the current user's identity to enforce project ownership. These services are registered in `ServiceCollectionExtensions` and can be tested without an HTTP request.

### Persistence

`Persistence` contains `ApplicationDbContext`, entity configurations, the EF Core migration, development seeding, and an EF trigger for entity timestamps. The context uses PostgreSQL through Npgsql and includes ASP.NET Core Identity tables. It applies entity configurations from its assembly. Domain entities remain separate from the database mappings. The existing migration covers Identity, Products, Projects, and Technicians.

### Server and HTTP responses

`Server` exposes FastEndpoints routes and hosts the Blazor WebAssembly files. Endpoints delegate use-case work to services and use shared contracts for input and output. Authentication uses ASP.NET Core Identity with cookies; authorization is enforced on protected endpoints and again in the project-editing service where ownership matters. Swagger is available through the server configuration.

In `Program.cs`, FastEndpoints is configured with `DontAutoSendResponse()`. The `GlobalResponseSender` post-processor sends the response centrally, mapping `Ardalis.Result` statuses to HTTP responses. For example, successful results, invalid requests, and missing resources become the appropriate success, client-error, or not-found response. The request and response processors also provide logging around endpoint execution. Keep this setup when adding endpoints, because endpoints rely on the central sender.

### Client

`Client` is a Blazor WebAssembly application served by `Server`. It has an HTTP client for API calls, a cookie-aware authentication state provider, and pages that demonstrate public, authenticated, and Administrator-only UI. The Products pages show how a feature connects UI components to shared contracts and the API. The client base URL is configured in `wwwroot/appsettings.json` for local development.

### Tests and extension points

The test projects contain domain tests, service tests, and Blazor component tests with xUnit, Shouldly, NSubstitute, and bUnit. The project-editing service tests use EF Core's in-memory provider; this does not verify PostgreSQL behavior. Add database and API integration tests when a derived project needs that coverage.

To add a feature, put its business rules in `Domain`, its API contracts in `Shared`, its use-case logic in `Services`, its EF mapping in `Persistence`, and its HTTP endpoint in `Server`. Add client components only when the feature needs a UI. Register new services and add a migration for schema changes.

