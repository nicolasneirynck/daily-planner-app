# API success contract — PL-85

Services may return `Ardalis.Result` internally. Public `/api` success responses
contain only the documented resource DTO or no body. Errors continue to use the
existing Problem Details contract in `docs/api-errors.md`.

## Current endpoints

| Request | Success status | Body | Response headers |
| --- | --- | --- | --- |
| `GET /api/day-planning/{date}` | 200 | `DayPlanningResponse` as direct JSON | `Content-Type: application/json` |
| `POST /api/day-planning/activities` | 201 | `PlannedActivityDto` as direct JSON (`id`, `name`, `description`, `isCompleted`) | `Content-Type: application/json`; `Location: /api/day-planning/activities/{id}` |
| `PUT /api/day-planning/activities/{activityId}/completion` | 204 | Empty | No JSON content type |
| `DELETE /api/day-planning/activities/{plannedActivityId}` | 204 | Empty | No JSON content type |
| `GET /api/identity/accounts/info` | 200 | `AccountResponse.Info` as direct JSON | `Content-Type: application/json` |
| `POST /api/identity/accounts/login` | 204 | Empty | Authentication cookie where applicable; no JSON content type |
| `POST /api/identity/accounts/logout` | 204 | Empty | Cookie-clearing header where applicable; no JSON content type |
| `POST /api/identity/accounts/register` | 201 | Direct JSON object with `id` and `email`; no password or claims | `Content-Type: application/json`; `Location: /api/identity/accounts/{id}` |
| `GET /api/identity/roles` | 200 | Direct JSON array of `{key, value}` objects, containing role ID and name | `Content-Type: application/json` |
| `POST /api/identity/roles` | 201 | Direct JSON object with `id` and `name` | `Content-Type: application/json`; `Location: /api/identity/roles/{id}` |

The activity `Location` identifies the created resource at the existing
`DELETE /api/day-planning/activities/{id}` URI. The day-planning response
already includes the activity DTO, so a separate activity GET route is not
needed for the current client. Account and role `Location` values likewise
identify the created resources; no GET endpoint for either URI currently
exists. Unauthorized, forbidden and missing-resource responses follow the
Problem Details contract.

A 204 response has zero body bytes. It does not serialize `null`, `{}` or an
`Ardalis.Result` object. A 201 response contains the created resource
representation without an `Ardalis.Result` wrapper.

## Consumers and migration

Repository production callers found: `DayPlanningApiClient` calls the
day-planning routes; `CookieAuthenticationStateProvider` calls the account
routes. No repository production caller of the role routes was found. External
consumers, their owners and their deployment schedules **cannot be determined
from repository inspection**.

This is a breaking change for callers that deserialize `Ardalis.Result`
success bodies. Before deployment, record and notify any externally known API
consumers through the project's deployment channel. Release the server and
hosted Blazor client together, publish the new status/body/header table in the
release notes, and monitor client failures after deployment. If external
consumers require a transition period, introduce a separately versioned API
before switching the existing contract; do not silently keep an undocumented
envelope on `/api`. Roll back the server and client together if consumers
cannot migrate in time.

## Verification

Server contract tests assert status, body shape and headers for every current
endpoint. In particular, they assert zero body bytes for 204 and the exact
resource-specific `Location` for each 201. Client tests assert direct DTO
deserialization and bodyless success handling. Existing
Problem Details tests continue to cover failures.
