using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planning.Client.Http;

internal static class ApiResponseReader
{
    public static async Task<ClientResult> ReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (response.StatusCode == HttpStatusCode.NoContent)
            return new ClientResult(response.StatusCode);

        if (!response.IsSuccessStatusCode)
            return await ReadFailureAsync(response, cancellationToken);

        _ = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        return new ClientResult(response.StatusCode);
    }

    public static async Task<ClientResult<T>> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (response.StatusCode == HttpStatusCode.NoContent)
            return new ClientResult<T>(response.StatusCode, default);

        if (!response.IsSuccessStatusCode)
        {
            var failure = await ReadFailureAsync(response, cancellationToken);

            return new ClientResult<T>(failure.StatusCode, default, failure.ErrorMessage) // default -> vult de plek van Value in. Bij een mislukte aanvrag is er immers geen succeswaarde. Voor DayPlanningResponse betekent default null
            {
                FieldErrors = failure.FieldErrors
            };
        }

        var body = await response.Content.ReadFromJsonAsync<ApiSuccessEnvelope<T>>(
            cancellationToken: cancellationToken);

        if (body is null || body.Value is null)
            throw new InvalidOperationException("API response was empty.");

        return new ClientResult<T>(response.StatusCode, body.Value);
    }

    private sealed record ApiSuccessEnvelope<T>(T? Value);

    private static async Task<ClientResult> ReadFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ApiProblem? problem = null;

        if (response.Content.Headers.ContentType?.MediaType ==
            "application/problem+json")
        {
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken: cancellationToken); // zet JSON zoals {"detail":"The request is invalid.","errors":[...]} om naar een ApiProblem-object
            }
            catch (JsonException)
            {
                // Use the HTTP status when the error body is unreadable.
            }
        }

        var message = string.IsNullOrWhiteSpace(problem?.Detail)
            ? $"The request failed with HTTP {(int)response.StatusCode}."
            : problem.Detail;

        IReadOnlyList<ClientFieldError> fieldErrors =
            response.StatusCode == HttpStatusCode.BadRequest
                ? problem?.Errors?
                    .Where(error => !string.IsNullOrWhiteSpace(error.Reason))
                    .Select(error => new ClientFieldError(
                        string.IsNullOrWhiteSpace(error.Name) ? "$" : error.Name,
                        error.Reason!))
                    .ToArray() ?? [] // als "problem" of Erros null is -> gebruik een lege lijst
                : []; // als de status geen 400 is gebruik ook een lege lijst

        return new ClientResult(response.StatusCode, message)
        {
            FieldErrors = fieldErrors
        };
    }

}
