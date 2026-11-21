using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planning.Client.Http;

internal static class ApiResponseReader
{
    public static async Task<ApiCallResult<T>> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NoContent)
                return new(response.StatusCode, default, null);

            var value = await response.Content.ReadFromJsonAsync<T>(
                cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("API response was empty.");

            return new(response.StatusCode, value, null);
        }

        ApiProblem? problem = null;
        if (response.Content.Headers.ContentType?.MediaType ==
            "application/problem+json")
        {
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ApiProblem>(
                    cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                // The adapter supplies a status-based message.
                // enkel commentaar omdat ongeldige JSON in een foutrespons de client niet nog eens moet laten vastlopen
                /*
                 * Als ReadFromJsonAsync een JsonException geeft, blijft problem op null staan. De code na dit blok gebruikt dan de HTTP-status om een algemene foutmelding te maken.
                   De lege catch zegt dus bewust: “Ik kan deze foutbody niet lezen; ga verder met de terugvalmelding.”
                 */
            }
        }

        return new(response.StatusCode, default, problem);
    }
}
