# Architecture

## Architecture overview

Daily Planner is a .NET 10 application with a Blazor WebAssembly frontend, an ASP.NET Core server, and a PostgreSQL database. The server serves the client files and exposes a same-origin HTTP API implemented with FastEndpoints. Planning requests pass through API endpoints to application services; those services use Entity Framework Core (EF Core) to read and write the database. ASP.NET Core Identity handles accounts and roles in the same database. See [server startup](../src/Planning.Server/Program.cs) and the [client startup](../src/Planning.Client/Program.cs).

The solution separates the browser UI, HTTP endpoints, application operations, domain entities, persistence configuration, and shared request/response contracts into projects. This is a description of the current code boundaries, not a claim that every dependency points inward: the service project directly references the persistence project and uses its `ApplicationDbContext`.

## Solution structure and responsibilities

| Project | Current responsibility |
| --- | --- |
| [`Planning.Client`](../src/Planning.Client/) | Blazor pages and components, browser authentication state, and HTTP clients for the API. |
| [`Planning.Server`](../src/Planning.Server/) | ASP.NET Core host, FastEndpoints endpoints, Identity operations, authorization, response handling, and static client hosting. |
| [`Planning.Shared`](../src/Planning.Shared/) | Request and response types shared by the browser and server, plus identity role names and some validators. |
| [`Planning.Services`](../src/Planning.Services/) | Daily planning operations, current-user checks, and initialization of default time blocks. |
| [`Planning.Domain`](../src/Planning.Domain/) | Planning entities and invariants, including planned activity name and description limits. |
| [`Planning.Persistence`](../src/Planning.Persistence/) | EF Core context, entity and Identity mappings, migrations, save triggers, and development data seeding. |

The [project references](../src/Planning.Server/Planning.Server.csproj) make `Planning.Server` the composition root: it references the other production projects and registers their implementations. `Planning.Client` references `Planning.Shared`; `Planning.Persistence` references `Planning.Domain`; and `Planning.Services` references `Planning.Domain`, `Planning.Persistence`, and `Planning.Shared`. The domain and shared projects have no project references to the application or infrastructure projects.

## Request and data flow

The [day planning page](../src/Planning.Client/Pages/DayPlanning.razor.cs) calls `IDayPlanningClient`. Its [HTTP implementation](../src/Planning.Client/DayPlanning/DayPlanningApiClient.cs) sends JSON requests to `/api/day-planning` routes and reads responses through [`ApiResponseReader`](../src/Planning.Client/Http/ApiResponseReader.cs). The client and API exchange the DTOs and request types in [`Planning.Shared/DayPlanning`](../src/Planning.Shared/DayPlanning/).

For a planning request, a [FastEndpoints endpoint](../src/Planning.Server/Endpoints/DayPlanning/) extracts route or body data and calls `IDayPlanningService`. [`DayPlanningService`](../src/Planning.Services/DayPlanning/DayPlanningService.cs) obtains the current user through `ISessionContextProvider`, queries or updates `ApplicationDbContext`, and returns an `Ardalis.Result`. The [global response processor](../src/Planning.Server/Processors/GlobalResponseSender.cs) translates that result into an HTTP success response or the API error format. A day read loads the user's time blocks and activities for the selected date, then assembles activities under their blocks for the response. Create, update, completion, and delete operations persist changes through EF Core.

Identity endpoints follow a shorter path: they call ASP.NET Core Identity managers directly. Registration also calls [`DefaultTimeBlockInitializer`](../src/Planning.Services/TimeBlocks/DefaultTimeBlockInitializer.cs) in a database transaction so a new user receives six time blocks. See the [account endpoints](../src/Planning.Server/Endpoints/Identity/Accounts/).

## Authentication and authorization

The server configures ASP.NET Core Identity with EF Core stores and authentication/authorization middleware in [`Program.cs`](../src/Planning.Server/Program.cs). Login uses `SignInManager` with the Identity application cookie; logout signs out. In the browser, the named `SecureApi` HTTP client uses [`CookieHandler`](../src/Planning.Client/Identity/CookieHandler.cs) to include browser credentials. [`CookieAuthenticationStateProvider`](../src/Planning.Client/Identity/CookieAuthenticationStateProvider.cs) calls the account-info endpoint and builds the Blazor principal from returned claims and roles.

Registration and login allow anonymous requests. Other FastEndpoints endpoints use the framework's default authorization behavior unless configured otherwise; role management endpoints explicitly require the `Administrator` role. The client has authorized example routes, while the day planning page itself has no `[Authorize]` attribute. Regardless of client routing, the planning service requires a current user ID and scopes its time block and planned activity queries to that user. See the [planning endpoints](../src/Planning.Server/Endpoints/DayPlanning/), [role endpoints](../src/Planning.Server/Endpoints/Identity/Roles/), and [planning service](../src/Planning.Services/DayPlanning/DayPlanningService.cs).

## Data persistence

[`ApplicationDbContext`](../src/Planning.Persistence/ApplicationDbContext.cs) derives from `IdentityDbContext<IdentityUser>` and exposes `Activity`, `TimeBlock`, and `PlannedActivity` sets alongside Identity tables. EF Core mappings in [`Configurations`](../src/Planning.Persistence/Configurations/) define relationships, constraints, and indexes. `PlannedActivity` stores a date, time block, name, description, completion flag, and importance flag; it also has an optional link to `Activity`. The planning workflow currently creates and edits planned activities directly.

User ownership is represented by `UserId` on planning entities. The planned activity mapping uses a composite foreign key from `(TimeBlockId, UserId)` to a time block's `(Id, UserId)`, and indexes `(UserId, Date)`. Default time blocks are stored per user, rather than calculated only in the UI. See the [entity mappings](../src/Planning.Persistence/Configurations/PlannedActivities/PlannedActivityConfiguration.cs) and [initializer](../src/Planning.Services/TimeBlocks/DefaultTimeBlockInitializer.cs).

The common entity base has timestamps and an `IsDeleted` flag. A [before-save trigger](../src/Planning.Persistence/Triggers/EntityBeforeSaveTrigger.cs) updates timestamps and converts tracked EF deletion into a soft delete. The current planning service's delete operation instead uses `ExecuteDeleteAsync`, which deletes the database row directly and bypasses that trigger. Planning reads filter out rows marked as deleted. In `Development`, server startup applies [EF Core migrations](../src/Planning.Persistence/Migrations/) and seeds demo accounts and planning data; the startup code does not run that migration and seeding block in other environments.

## Validation and error handling

The activity form uses a local model with data annotations for immediate browser validation. FastEndpoints uses FluentValidation for planning create/update requests and the validators defined in shared identity request types. [`PlannedActivityRules`](../src/Planning.Domain/PlannedActivities/PlannedActivityRules.cs) supplies the planning text limits used by the API validators and domain entity. The service also checks those limits and verifies that a requested time block belongs to the current user before saving. See the [form model](../src/Planning.Client/DayPlanning/ActivityFormModel.cs), [endpoint validators](../src/Planning.Server/Endpoints/DayPlanning/), and [service](../src/Planning.Services/DayPlanning/DayPlanningService.cs).

The API maps application results, validation failures, authorization failures, missing API routes, and unhandled API exceptions to `application/problem+json` responses. Validation errors can include field names and reasons. The browser response reader turns those errors into client results, which the form can display. The error contract is described separately in [API errors](api-errors.md); the implementation is in [`ApiProblemResponse`](../src/Planning.Server/Errors/ApiProblemResponse.cs), [`GlobalResponseSender`](../src/Planning.Server/Processors/GlobalResponseSender.cs), and the [client response reader](../src/Planning.Client/Http/ApiResponseReader.cs). Server request and response processors use Serilog for API logging.

## Testing architecture

The solution has separate [domain](../tests/Planning.Domain.Tests/), [service](../tests/Planning.Services.Tests/), [server](../tests/Planning.Server.Tests/), [client](../tests/Planning.Client.Tests/), and [end-to-end](../tests/Planning.E2E.Tests/) test projects. Domain tests exercise entity rules. Service tests exercise planning operations against temporary PostgreSQL instances via Testcontainers. Server tests use `WebApplicationFactory` to verify endpoint, identity, and error behavior; planning endpoint tests can replace the planning service with a test implementation. Client tests use bUnit for components and test the API response reader and authentication state provider. Playwright end-to-end tests run the hosted application with a temporary database and exercise browser planning journeys. These are the test layers present in the repository, rather than a guarantee of coverage for every behavior.
