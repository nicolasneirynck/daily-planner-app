using System.Text.Json.Serialization;
using System.Text.Json;

namespace Planning.Server.Errors;

internal sealed class ApiProblemDetails
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("status")]
    public required int Status { get; init; }

    [JsonPropertyName("detail")]
    public required string Detail { get; init; }

    [JsonPropertyName("instance")]
    public required string Instance { get; init; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ApiFieldError>? Errors { get; init; }
}

/* BV:
 * 1. De code maakt de veldfout:
    var error = new ApiFieldError("name", "Activity name is required.");
   
   2. C# bewaart de twee waarden in de eigenschappen van het record:
    error.Name   // "name"
    error.Reason // "Activity name is required."
   
   3. Wanneer de API error naar JSON omzet, ziet de JSON-schrijver het label op de eigenschap Name:
    [property: JsonPropertyName("name")] string Name
   
   4.De client ontvangt:
   {
     "name": "name",
     "reason": "Activity name is required."
   }
 */
internal sealed record ApiFieldError(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("reason")] string Reason)
{
    // hulpmethode om 1 veldfout aan te maken, zet de veldnaam om in de afgesproken vorm
    public static ApiFieldError From(string? name, string reason) =>
        new(
            string.IsNullOrWhiteSpace(name)
                ? "$" // -> de fout hoort bij de aanvraag als geheel, niet tot een specifiek veld
                : JsonNamingPolicy.CamelCase.ConvertName(name),
            reason);
}
    
