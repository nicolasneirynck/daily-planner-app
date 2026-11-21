using System.Net;

namespace Planning.Client.Http;

/*
 * Dit bestand definieert hoe de Blazor-client het resultaat van een API-aanroep bewaart.
 */
public sealed record ClientFieldError(string Name, string Message); // bewaart één validatiefout: de veldnaam (Name, bijvoorbeeld "email") en de melding (Message).

public sealed record ClientResult( // bevat geen waarde. Dat past bij acties zoals verwijderen, uitloggen of een activiteit opslaan, waarbij de UI alleen wil weten of het gelukt is en welke fouten er zijn.
    HttpStatusCode StatusCode, string? ErrorMessage = null)
{
    public IReadOnlyList<ClientFieldError> FieldErrors { get; init; } = [];

    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;
}

public sealed record ClientResult<T>( // ClientResult<T> bewaart het resultaat van een aanvraag. T is het type van de succeswaarde, bijvoorbeeld DayPlanningResponse. Wordt gebruikt als succes een waarde heeft, bv dagplanning ophalen
    HttpStatusCode StatusCode,
    T? Value,
    string? ErrorMessage = null)
{
    public IReadOnlyList<ClientFieldError> FieldErrors { get; init; } = [];

    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;
}
