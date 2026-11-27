using System.Globalization;
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

    [SupplyParameterFromQuery(Name = "date")]
    public string? DateQuery { get; set; }

    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    private PlannedActivityDto? _activity;
    private string? _timeBlockName;
    private bool _isLoading = true;
    private string? _loadError;
    private int _loadVersion;
    private bool _isUpdatingCompletion;
    private bool _completionError;
    private bool _showDeleteConfirmation;
    private bool _isDeleting;
    private string? _deleteError;

    private string DeleteConfirmationMessage =>
        $"“{_activity?.Name}” wordt verwijderd uit je dagplanning.";

    protected override async Task OnParametersSetAsync()
    {
        var loadVersion = ++_loadVersion;
        _isLoading = true;
        _loadError = null;
        _activity = null;
        _timeBlockName = null;
        _isUpdatingCompletion = false;
        _completionError = false;
        _showDeleteConfirmation = false;
        _isDeleting = false;
        _deleteError = null;

        _date = DateOnly.TryParseExact(DateQuery, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var linkDate)
            ? linkDate
            : DateOnly.FromDateTime(DateTime.Today);
        var requestedActivityId = ActivityId;

        try
        {
            var result = await DayPlanningClient.GetActivityAsync(requestedActivityId);

            if (loadVersion != _loadVersion) // de controle moet na de vorige await staan. Omdat de toestand kan veranderen terwijl GetActivityAsync loopt. Bijvoorbeeld: aanvraag A start met loadVersion = 1. Tijdens het wachten start aanvraag B, die _loadVersion op 2 zet. Wanneer A terugkomt, ziet de controle 1 != 2 en negeert ze het verouderde antwoord.
                return;

            if (!result.IsSuccess || result.Value is null)
            {
                _loadError = result.StatusCode == HttpStatusCode.NotFound
                    ? "Deze activiteit staat niet meer in de dagplanning."
                    : "De activiteit kon niet geladen worden.";
                return;
            }

            var details = result.Value;
            _date = details.Date;
            _timeBlockName = details.TimeBlockName;
            _activity = details.ToPlannedActivity();
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            if (loadVersion == _loadVersion)
            {
                Log.Warning(ex, "Could not load activity details for {ActivityId}", requestedActivityId);
                _loadError = "De activiteit kon niet geladen worden.";
            }
        }
        finally
        {
            if (loadVersion == _loadVersion)
                _isLoading = false;
        }
    }

    private async Task ToggleCompletionAsync(PlannedActivityDto activity)
    {
        if (_isUpdatingCompletion || _activity is null || activity.Id != _activity.Id)
            return;

        var loadVersion = _loadVersion;
        _isUpdatingCompletion = true;
        _completionError = false;
        var newStatus = !activity.IsCompleted;

        try
        {
            var result = await DayPlanningClient.SetActivityCompletionAsync(
                activity.Id, newStatus);

            if (loadVersion != _loadVersion)
                return;

            if (result.IsSuccess)
            {
                activity.IsCompleted = newStatus;
                return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            Log.Warning(ex, "Could not update completion for activity {ActivityId}", activity.Id);
        }
        finally
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
        if (_isDeleting || _activity is null)
            return;

        var loadVersion = _loadVersion;
        var activityId = _activity.Id;
        _isDeleting = true;
        _deleteError = null;

        try
        {
            var result = await DayPlanningClient.DeletePlannedActivityAsync(activityId);

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
            Log.Warning(ex, "Could not delete activity {ActivityId}", activityId);
        }
        finally
        {
            if (loadVersion == _loadVersion)
                _isDeleting = false;
        }

        if (loadVersion == _loadVersion)
            _deleteError = "Verwijderen is niet gelukt. Probeer het opnieuw.";
    }

    private void OpenEdit() =>
        Navigation.NavigateTo($"/dayplanning/activities/{ActivityId}/edit?date={_date:yyyy-MM-dd}");

    private void ReturnToDay() =>
        Navigation.NavigateTo(DayUrl(_date), replace: true);

    private static string DayUrl(DateOnly date) =>
        $"/dayplanning?date={date:yyyy-MM-dd}";
}
