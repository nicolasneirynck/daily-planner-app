# API Response Contract

Daily Planner exposes a same-origin HTTP API under `/api`.

The API returns resource data as JSON for successful requests, empty responses for operations that do not return content, and standardized Problem Details responses for errors.

Application services use `Ardalis.Result` internally, while the HTTP layer converts these results into appropriate HTTP responses.

## Successful Responses

Successful API requests return either a JSON response body or an empty response, depending on the operation.

### Day Planning

| Request | Status | Response Body | Additional Header |
|---|---|---|---|
| `GET /api/day-planning/{date}` | 200 | `DayPlanningResponse` | — |
| `POST /api/day-planning/activities` | 201 | `PlannedActivityDto` | `Location` |
| `PUT /api/day-planning/activities/{activityId}` | 204 | Empty | — |
| `PUT /api/day-planning/activities/{activityId}/completion` | 204 | Empty | — |
| `DELETE /api/day-planning/activities/{plannedActivityId}` | 204 | Empty | — |

### Identity & Roles

| Request | Status | Response Body | Additional Header |
|---|---|---|---|
| `GET /api/identity/accounts/info` | 200 | `AccountResponse.Info` | — |
| `POST /api/identity/accounts/login` | 204 | Empty | Authentication cookie |
| `POST /api/identity/accounts/logout` | 204 | Empty | Cookie-clearing header |
| `POST /api/identity/accounts/register` | 201 | Account ID and email | `Location` |
| `GET /api/identity/roles` | 200 | Role ID/name pairs | — |
| `POST /api/identity/roles` | 201 | Role ID and name | `Location` |

### Response Conventions

- Successful responses with a body use `application/json`.
- `204 No Content` responses have no response body.
- JSON responses contain the resource data directly, without an `Ardalis.Result` wrapper.
- `201 Created` responses include a `Location` header identifying the created resource. Dedicated GET endpoints are not currently available for all these resource URIs.


## Error Responses

The API uses the Problem Details format (`application/problem+json`) to provide consistent error responses.

This applies to request validation failures, unsuccessful application results, authentication and authorization errors, unmatched API routes, and unhandled API exceptions.

### HTTP Status Codes

| HTTP Status | Description |
|---|---|
| `400 Bad Request` | Request validation failure or `Result.Invalid`. |
| `401 Unauthorized` | Authentication required or `Result.Unauthorized`. |
| `403 Forbidden` | Insufficient permissions or `Result.Forbidden`. |
| `404 Not Found` | Resource not found, unmatched API route, or `Result.NotFound`. |
| `409 Conflict` | `Result.Conflict`. |
| `422 Unprocessable Content` | `Result.Error`. |
| `500 Internal Server Error` | `Result.CriticalError` or an unhandled API exception. |
| `503 Service Unavailable` | `Result.Unavailable`. |

### Error Response Format

Each error response contains the following properties:

| Property | Description |
|---|---|
| `type` | Set to `about:blank`. |
| `title` | Standard HTTP status description. |
| `status` | Numeric HTTP status code. |
| `detail` | A generic message associated with the HTTP status. |
| `instance` | A unique `urn:uuid` identifier, also recorded in server logs. |
| `errors` | Optional array of field-specific validation errors. |

Validation errors contain `name` and `reason` properties. Field names use camelCase, while `$` represents a request-level error.

Internal exception details and application error messages are not automatically exposed to clients.


## Error Response Example

The following example illustrates a validation error returned when an activity name is missing.

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
    {
      "name": "name",
      "reason": "Activity name is required."
    }
  ]
}
```

The `instance` identifier is generated for each error and can be used to locate the corresponding server log entry. The identifier shown above is illustrative.

The Blazor client interprets the response and can display field-specific validation messages to the user.
