# API error contract (PL-78)

This document records the current error paths and defines the target error
contract for requests under `/api`. It is a contract decision, not a description
of behavior already implemented. Blazor pages and static files are outside its
scope. Services may continue to use `Ardalis.Result` internally, and successful
HTTP responses retain their current content, including responses without a body.

## Current error paths

The statuses and bodies below follow from the current code and framework
configuration. Where middleware behavior has not been observed in a running
server, it is marked for verification rather than presented as a measured
response. Products and Projects are template features and are excluded from
this inventory.

| Source and example | Current HTTP status | Current response body | Known consumer |
| --- | --- | --- | --- |
| FastEndpoints request validation, such as an invalid activity name or route date | 400 | FastEndpoints validation JSON with `statusCode`, `message`, and an `errors` object keyed by field name; verify the exact serialized shape and content type | `DayPlanningApiService` |
| `DayPlanningService` returns `Result.Invalid` | 400 | Serialized `Ardalis.Result` with `validationErrors` | `DayPlanningApiService` |
| `DayPlanningService` returns `Result.Unauthorized` or `Result.NotFound` | 401 or 404 | Serialized `Ardalis.Result` | `DayPlanningApiService` |
| Login rejects credentials, or account info cannot find the user | 401 or 404 | Serialized `Ardalis.Result` | `CookieAuthenticationStateProvider` |
| Registration returns `Result.Error` or `Result.CriticalError` | 422 or 500 | Serialized `Ardalis.Result`; the current messages may contain Identity or internal failure details | `CookieAuthenticationStateProvider` |
| A protected endpoint is challenged or forbidden before it executes | 401 or 403 | Authentication/authorization middleware response, not the global `Ardalis.Result` response; verify the exact body and content type | `CookieAuthenticationStateProvider` for account info; `DayPlanningApiService` for protected day-planning routes |
| No API route matches, such as `/api/unknown` | Likely 200 for a path without a filename because `MapFallbackToFile("index.html")` also matches it; verify with HTTP | Likely the Blazor HTML shell; paths treated as filenames may instead yield 404 | No repository production consumer found |
| An unhandled exception during an API request | 500 | FastEndpoints `UseDefaultExceptionHandler()` response; verify its exact body and content type | Any API consumer |

`GlobalResponseSender` maps other `Ardalis.Result` failures to 403
(`Forbidden`), 409 (`Conflict`), 422 (`Error`), 500 (`CriticalError`), and 503
(`Unavailable`). These currently serialize the result object too. Identity role
endpoints can return `Conflict` or `Error`, but no production client for them
was found in this repository. The response processor also preserves success
results: `Ok` becomes 200, `Created` becomes 201, and `NoContent` becomes 204.

The repository contains no other production HTTP caller of `/api` besides
`DayPlanningApiService` and `CookieAuthenticationStateProvider` after excluding
the Product template client. Browser-driven end-to-end tests call the API as
tests, not as a separate production client. External consumers cannot be ruled
out from repository inspection and remain unknown.

### Current client behavior

- `DayPlanningApiService` deserializes GET responses directly into
  `Result<DayPlanningResponse>`. For POST, PUT, and DELETE it calls
  `EnsureSuccessStatusCode()` before deserializing `Result`. Consequently,
  error bodies are not read consistently. The day-planning page currently
  shows generic save, delete, and completion messages rather than field errors.
- `CookieAuthenticationStateProvider` deserializes registration and login
  responses into `Result` and displays their `Errors` on the corresponding
  forms. The account-info request uses `GetFromJsonAsync`, which throws on an
  unsuccessful status. A 401 leaves the user anonymous, but the provider's
  general exception handler currently logs it as an error. Logout does not
  inspect the response body.
- The `CookieHandler` sends cookies and the `X-Requested-With: XMLHttpRequest`
  header. ASP.NET Core 10 returns 401/403 for these requests instead of a
  cookie-login redirect, but this does not give them the target Problem Details
  body.

## Target contract

Every error response under `/api` uses `Content-Type: application/problem+json`
and a JSON object with the following camelCase
members. These five members are required by this API, even where RFC 9457
would allow them to be absent.

| Member | Meaning |
| --- | --- |
| `type` | URI identifying the problem type. Use `about:blank` for the currently defined errors, including validation. It means the HTTP status supplies the general problem semantics. Clients must not infer a finer category from this value. |
| `title` | Stable, human-readable HTTP status title, such as `Bad Request` or `Not Found`. It does not vary by occurrence. |
| `status` | Numeric HTTP status code, equal to the actual response status. |
| `detail` | Safe, human-readable explanation of this occurrence. It is not a machine-readable discriminator and must not contain exception, stack-trace, or other internal details. |
| `instance` | A unique `urn:uuid:<uuid>` URI for this error occurrence. The server logs the same identifier for correlation; the URI is opaque and is not a retrieval endpoint. |

Validation failures additionally have an `errors` array. Each element has
`name`, the camelCase JSON request-property name or route/query parameter name,
and `reason`, a human-readable explanation for that field. Use `name: "$"` for
a request-level validation error without a specific field. Multiple failures
for one field remain separate array entries. Clients use `errors` when present
to show field messages and use `detail` for a general message. The `errors`
extension does not require a separate `type` URI. A new type URI should only be
added when the API defines stable, distinct semantics that clients need.

When a service supplies no usable detail, the API supplies a safe default for
the status. For validation, `detail` summarizes that the request is invalid;
the individual reasons are in `errors`. For 401 and 403, the text does not
reveal account or permission details. For 404, the text does not reveal whether
a protected resource exists. For unexpected errors and internal
`CriticalError` results, use a generic 500 detail and log the technical cause
server-side. Service-provided text is not automatically safe to expose,
especially Identity and internal failure messages.

The API boundary converts non-success `Ardalis.Result` values to this contract.
Endpoint validation, authentication challenges, authorization forbids,
unmatched `/api` routes, and unhandled exceptions use the same contract.
Unauthenticated account-info requests remain HTTP 401 so that the authentication
client can treat them as the expected anonymous state. The HTTP status and body
`status` always match. No error response contains an `Ardalis.Result` body.

## References and verification

- [RFC 9457: Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457.html)
- [FastEndpoints validation responses](https://fast-endpoints.com/docs/validation)
- [ASP.NET Core 10 cookie API behavior](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/cookie-authentication-api-endpoints?view=aspnetcore-10.0)
- [ASP.NET Core SPA fallback routing](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.staticfilesendpointroutebuilderextensions.mapfallbacktofile?view=aspnetcore-10.0)

The current middleware body/content-type and unmatched-route behavior should
be confirmed with HTTP requests when running the server. PL-79 implements the
target contract, PL-80 updates the clients, and PL-81 adds contract tests and
request/response examples to the README and API documentation.
