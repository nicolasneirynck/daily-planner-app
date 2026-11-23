# API response contract

The ASP.NET Core server hosts the Blazor WebAssembly client and exposes a same-origin API under `/api`. Endpoints and services can return `Ardalis.Result` internally; the HTTP boundary sends a resource JSON body, an empty success response, or a Problem Details error. The response mapping is implemented in [`GlobalResponseSender`](../src/Planning.Server/Processors/GlobalResponseSender.cs).

## Successful responses

| Request | Status | Response body | Additional response header |
| --- | --- | --- | --- |
| `GET /api/day-planning/{date}` | 200 | `DayPlanningResponse` JSON | — |
| `POST /api/day-planning/activities` | 201 | `PlannedActivityDto` JSON | `Location: /api/day-planning/activities/{id}` |
| `PUT /api/day-planning/activities/{activityId}` | 204 | Empty | — |
| `PUT /api/day-planning/activities/{activityId}/completion` | 204 | Empty | — |
| `DELETE /api/day-planning/activities/{plannedActivityId}` | 204 | Empty | — |
| `GET /api/identity/accounts/info` | 200 | `AccountResponse.Info` JSON | — |
| `POST /api/identity/accounts/login` | 204 | Empty | Authentication cookie |
| `POST /api/identity/accounts/logout` | 204 | Empty | Cookie-clearing header |
| `POST /api/identity/accounts/register` | 201 | Account ID and email JSON | `Location: /api/identity/accounts/{id}` |
| `GET /api/identity/roles` | 200 | JSON array of role ID/name pairs (`key`, `value`) | — |
| `POST /api/identity/roles` | 201 | Role ID and name JSON | `Location: /api/identity/roles/{id}` |

JSON success bodies use `application/json`. A 204 response has no body or JSON content type. Success bodies contain the DTO directly, without an `Ardalis.Result` wrapper. The `Location` headers identify created resources, but the API does not currently expose a `GET` endpoint at the activity, account, or role `Location` URI. See the [planning endpoints](../src/Planning.Server/Endpoints/DayPlanning/) and [identity endpoints](../src/Planning.Server/Endpoints/Identity/).

`DayPlanningResponse` contains a date and time blocks with their planned activities. A `PlannedActivityDto` contains `id`, `name`, `description`, `isCompleted`, and `isImportant`; see the [shared day-planning contracts](../src/Planning.Shared/DayPlanning/). The browser's [`DayPlanningApiClient`](../src/Planning.Client/DayPlanning/DayPlanningApiClient.cs) calls the planning routes, while [`CookieAuthenticationStateProvider`](../src/Planning.Client/Identity/CookieAuthenticationStateProvider.cs) calls the account routes. There is no production caller of the role routes in this repository.

## Error responses

API errors use Problem Details JSON with the `application/problem+json` media type. This applies to API request validation, failed `Ardalis.Result` values, authentication and authorization failures, unmatched `/api` routes, and unhandled API exceptions caught before a response starts. It does not describe Blazor pages or static files. See [server middleware](../src/Planning.Server/Program.cs) and [`ApiProblemResponse`](../src/Planning.Server/Errors/ApiProblemResponse.cs).

| Source or result | HTTP status |
| --- | --- |
| Request validation or `Result.Invalid` | 400 |
| Authentication challenge or `Result.Unauthorized` | 401 |
| Authorization forbid or `Result.Forbidden` | 403 |
| Missing resource, unmatched API route, or `Result.NotFound` | 404 |
| `Result.Conflict` | 409 |
| `Result.Error` | 422 |
| `Result.CriticalError` or caught unhandled API exception | 500 |
| `Result.Unavailable` | 503 |

Each error body has `type` (`about:blank`), `title` (the HTTP status title), `status` (the numeric HTTP status), `detail` (a status-based message), and `instance` (a generated `urn:uuid:<uuid>` identifier also logged by the server). Validation errors additionally have an `errors` array of `{ "name": "...", "reason": "..." }` entries. Field names use camelCase; `$` denotes a request-level error. The API does not automatically expose service error messages or exception details in `detail`.

For example, an invalid activity name can produce:

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "about:blank",
  "title": "Bad Request",
  "status": 400,
  "detail": "The request is invalid.",
  "instance": "urn:uuid:00000000-0000-0000-0000-000000000001",
  "errors": [
    { "name": "name", "reason": "Activity name is required." }
  ]
}
```

The example `instance` value is illustrative; the server generates a new value for each error. The browser's [`ApiResponseReader`](../src/Planning.Client/Http/ApiResponseReader.cs) reads `detail` and, for 400 responses, field errors for presentation in the UI. Server [API contract tests](../tests/Planning.Server.Tests/) and [client response tests](../tests/Planning.Client.Tests/Http/) exercise these response shapes.
