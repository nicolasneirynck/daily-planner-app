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
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter]
    public int ActivityId { get; set; }

    private PlannedActivityDetailsDto? _details;
    private bool _isLoading = true;
    private string? _loadError;
    private int _loadVersion;
    private bool _isUpdatingCompletion;
    private bool _completionError;
    private bool _showDeleteConfirmation;
    private bool _isDeleting;
    private string? _deleteError;

    private string DeleteConfirmationMessage =>
        $"“{_details?.Name}” wordt verwijderd uit je dagplanning.";

    private void ReturnToDay() =>
        Navigation.NavigateTo(
            $"/dayplanning?date={_details!.Date:yyyy-MM-dd}",
            replace: true);

    private void OpenEdit() =>
        Navigation.NavigateTo(
            $"/dayplanning/activities/{ActivityId}/edit?date={_details!.Date:yyyy-MM-dd}");

    private async Task ToggleCompletionAsync()
    {
        if (_isUpdatingCompletion || _details is null)
            return;

        var details = _details;
        var loadVersion = _loadVersion; // hier niet ++, _loadVersion telt de laadaanvragen voor activiteitdetails en ToggleCompletionAsync start geen nieuwe laadaanvraag. _isUpdatingCompletion bewaakt dubbele klikken al
        var newStatus = !details.IsCompleted;
        _isUpdatingCompletion = true;
        _completionError = false;

        try
        {
            var result = await DayPlanningClient.SetActivityCompletionAsync(details.Id, newStatus);
            if (loadVersion != _loadVersion)
                return;
            if (result.IsSuccess)
            {
                details.IsCompleted = newStatus;
                return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            Log.Warning(ex, "Could not update completion for activity {ActivityId}", details.Id);
        }
        finally // na de return van try nog uitgevoerd voor hij returned
        {
            if (loadVersion == _loadVersion)
                _isUpdatingCompletion = false;
        }

        if (loadVersion == _loadVersion)
            _completionError = true;
    }

    private void ShowDeleteConfirmation()
    {
        _deleteError = null;
        _showDeleteConfirmation = true;
    }

    private void CancelDeleteConfirmation() => _showDeleteConfirmation = false;

    private async Task DeleteAsync()
    {
        if (_isDeleting || _details is null)
            return;

        var details = _details;
        var loadVersion = _loadVersion;
        _isDeleting = true;
        _deleteError = null;

        try
        {
            var result = await DayPlanningClient.DeletePlannedActivityAsync(details.Id);
            if (loadVersion != _loadVersion)
                return;
            if (result.IsSuccess)
            {
                ReturnToDay();
                return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            Log.Warning(ex, "Could not delete activity {ActivityId}", details.Id);
        }
        finally
        {
            if (loadVersion == _loadVersion)
                _isDeleting = false;
        }

        if (loadVersion == _loadVersion)
            _deleteError = "Verwijderen is niet gelukt. Probeer het opnieuw.";
    }

    protected override async Task OnParametersSetAsync()
    {
        // Een nieuwere laadaanvraag maakt het antwoord van deze aanvraag ongeldig.
        // laatste actie van gebruiker beste =/= bv bij opslaan waar je de eerste verzoek wil laten primeren
        var loadVersion = ++_loadVersion;
        _isLoading = true;
        _loadError = null;
        _details = null;
        _isUpdatingCompletion = false;
        _completionError = false;
        _showDeleteConfirmation = false;
        _isDeleting = false;
        _deleteError = null;

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
