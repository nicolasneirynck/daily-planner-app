# API error contract

All error responses for requests under `/api` use Problem Details with the
`application/problem+json` media type. This contract does not apply to Blazor
pages or static files. Services may continue to use `Ardalis.Result`
internally; the API converts failures at the HTTP boundary. Successful
responses follow the contract in `docs/api-success-responses.md`.

## Error paths and consumers

The inventory covers the remaining API callers in this repository.

| Error path | HTTP status | Response body | Repository consumer |
| --- | --- | --- | --- |
| Endpoint request validation | 400 | Problem Details with `errors` | `DayPlanningApiClient`; `CookieAuthenticationStateProvider` for identity requests |
| `DayPlanningService` returns `Result.Invalid`, `Unauthorized`, or `NotFound` | 400, 401, or 404 | Problem Details; `Invalid` also has `errors` | `DayPlanningApiClient` |
| Identity account or role endpoint returns a failed `Ardalis.Result` | 401, 404, 409, 422, or 500, depending on the result | Problem Details | `CookieAuthenticationStateProvider` for account requests; no repository production caller found for role requests |
| Authentication challenge or authorization forbid before endpoint execution | 401 or 403 | Problem Details | `CookieAuthenticationStateProvider` for account information; `DayPlanningApiClient` for protected day-planning requests |
| No route matches under `/api` | 404 | Problem Details | No repository production caller found |
| Unhandled exception during an API request | 500 | Problem Details with a safe general `detail` | Any API caller |

The response processor also maps service `Conflict` to 409, `Error` to 422,
`CriticalError` to 500, and `Unavailable` to 503. All use the same Problem
Details shape. The repository has no other production `/api` caller. External consumers cannot be determined from
repository inspection.

## Response fields

The following camelCase members are required by this API, even where
RFC 9457 allows them to be omitted.

| Member | Meaning |
| --- | --- |
| `type` | URI identifying the problem type. Current errors, including validation, use `about:blank`. Clients use the HTTP status for the general meaning and must not infer a more specific category from this value. |
| `title` | Stable HTTP status title, such as `Bad Request` or `Not Found`. |
| `status` | Numeric HTTP status code. It equals the actual response status. |
| `detail` | Safe, human-readable explanation for this occurrence. It is not a machine-readable error code. |
| `instance` | Unique `urn:uuid:<uuid>` URI for this occurrence. The server logs the same identifier for correlation. It is opaque and is not a retrieval URL. |

Validation failures additionally contain an `errors` array. Every entry has
`name`, the camelCase JSON property name or route/query parameter name, and
`reason`, a human-readable explanation. A request-level error uses
`name: "$"`. Multiple failures for one field remain separate entries.

The API uses safe, status-based `detail` values. It does not expose service
messages, exception text, stack traces, account details, or permission details
automatically. Technical exception details remain in server logs. The shared
Blazor response reader retains `detail` and `errors`; the current UI shows
localized general messages and field errors where the form supports them.

An unauthenticated account-information request remains HTTP 401 so the
authentication client can treat it as an expected anonymous state. Error
responses never contain an `Ardalis.Result` body.

## Examples

Validation failure:

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

Unexpected failure:

```http
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json

{
  "type": "about:blank",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred.",
  "instance": "urn:uuid:00000000-0000-0000-0000-000000000002"
}
```

The `instance` values above are examples; the server generates a new value
for each error occurrence.

## Verification

Server integration tests cover validation, service errors, 401, 403, API 404,
and unexpected exceptions. Client tests cover field errors, general details,
and a successful response without a body.

## References

- [RFC 9457: Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457.html)
- [FastEndpoints validation responses](https://fast-endpoints.com/docs/validation)
