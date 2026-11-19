using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Planning.Server.Errors;

internal sealed class ApiProblemResponse(ILogger<ApiProblemResponse> logger)
{
    // gebruik de standaard JSON-instellingen voor webresponses
    // Web JSON-instellingen: bij het lezen worden bijvoorbeeld "status" en "Status" allebei herkend.
    // // Hier gebruiken we deze instellingen om de foutrespons als JSON te schrijven.
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    // maakt een Problem Details-object voor één foutrespons + schrijft een logregel. Verstuurt de HTTP-respons nog niet.
    public ApiProblemDetails Create(HttpContext context, int status, IReadOnlyList<ApiFieldError>? errors = null)
    {
        var instance = $"urn:uuid:{Guid.NewGuid():D}";

        logger.LogInformation(
            "API error {Status} at {Path}; instance {Instance}",
            status, context.Request.Path, instance);

        return new ApiProblemDetails
        {
            Type = "about:blank", // -> "about:blank" => deze fout heeft geen apart gedefinieerd fouttype; gebruik de HTTP-status voor de algemene betekenis.
            Title = ReasonPhrases.GetReasonPhrase(status), // standaardtitel bij de status, bv "Not Found" bij 404
            Status = status, // bv 404
            Detail = DefaultDetail(status), // kiest standaardmelding voor de gegeven HTTP-status
            Instance = instance,
            Errors = errors
        };
    }

    // verstuurt het Problem Details-object dat eerder gemaakt is
    // context bevat huidige HTTP-aanvraag en -respons
    public Task WriteAsync(HttpContext context, ApiProblemDetails problem, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = problem.Status; // stelt de echte HTTP-status in

        return context.Response.WriteAsJsonAsync(
            problem, JsonOptions, "application/problem+json", cancellationToken);
    }

    private static string DefaultDetail(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "The request is invalid.",
        StatusCodes.Status401Unauthorized => "Authentication is required.",
        StatusCodes.Status403Forbidden => "Access is forbidden.",
        StatusCodes.Status404NotFound => "The requested resource was not found.",
        StatusCodes.Status409Conflict => "The request conflicts with the current state.",
        StatusCodes.Status422UnprocessableEntity => "The request could not be processed.",
        StatusCodes.Status500InternalServerError => "An unexpected error occurred.",
        StatusCodes.Status503ServiceUnavailable => "The service is temporarily unavailable.",
        _ => "The request could not be completed."
    };
}
