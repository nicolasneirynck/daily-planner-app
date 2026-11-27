using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Planning.Client.DayPlanning;
using Planning.Shared.DayPlanning;
using Serilog;

namespace Planning.Client.Pages;

public partial class ActivityDetailsPage
{
    [Inject] private IDayPlanningClient DayPlanningClient { get; set; } = default!;

    [Parameter]
    public int ActivityId { get; set; }

    private PlannedActivityDetailsDto? _details;
    private bool _isLoading = true;
    private string? _loadError;
    private int _loadVersion;

    protected override async Task OnParametersSetAsync()
    {
        // Een nieuwere laadaanvraag maakt het antwoord van deze aanvraag ongeldig.
        // laatste actie van gebruiker beste =/= bv bij opslaan waar je de eerste verzoek wil laten primeren
        var loadVersion = ++_loadVersion;
        _isLoading = true;
        _loadError = null;
        _details = null;

        try
        {
            var result = await DayPlanningClient.GetActivityAsync(ActivityId);

            if (loadVersion != _loadVersion)
                return;

            if (result.IsSuccess && result.Value is not null)
                _details = result.Value;
            else
                _loadError = result.StatusCode == HttpStatusCode.NotFound
                    ? "Deze activiteit staat niet meer in de dagplanning."
                    : "De activiteit kon niet geladen worden.";
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            if (loadVersion == _loadVersion)
            {
                Log.Warning(ex, "Could not load activity details for {ActivityId}", ActivityId);
                _loadError = "De activiteit kon niet geladen worden.";
            }
        }
        finally
        {
            if (loadVersion == _loadVersion)
                _isLoading = false;
        }
    }
}
