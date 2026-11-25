using System.Globalization;
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
    private TimeBlockDto? _timeBlock;
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
        _timeBlock = null;
        _completionError = false;
        _showDeleteConfirmation = false;
        _deleteError = null;

        if (!DateOnly.TryParseExact(DateQuery, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _date))
        {
            _date = DateOnly.FromDateTime(DateTime.Today);
            _loadError = "De datum in de link is ongeldig.";
            _isLoading = false;
            return;
        }

        var requestedDate = _date;
        var requestedActivityId = ActivityId;

        try
        {
            var result = await DayPlanningClient.GetDayAsync(requestedDate);
            
            if (loadVersion != _loadVersion) // de controle moet na de vorige await staan. Omdat de toestand kan veranderen terwijl GetDayAsync loopt. Bijvoorbeeld: aanvraag A start met loadVersion = 1. Tijdens het wachten start aanvraag B, die _loadVersion op 2 zet. Wanneer A terugkomt, ziet de controle 1 != 2 en negeert ze het verouderde antwoord.
                return;

            if (!result.IsSuccess || result.Value is null)
            {
                _loadError = "De dagplanning kon niet geladen worden.";
                return;
            }

            foreach (var block in result.Value.TimeBlocks)
            {
                var activity = block.Activities.FirstOrDefault(
                    item => item.Id == requestedActivityId);
                if (activity is null)
                    continue;

                _activity = activity;
                _timeBlock = block;
                return;
            }

            _loadError = "Deze activiteit staat niet meer in de dagplanning.";
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            if (loadVersion == _loadVersion)
            {
                Log.Warning(ex, "Could not load activity details for {Date}", requestedDate);
                _loadError = "De dagplanning kon niet geladen worden.";
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

        _isUpdatingCompletion = true;
        _completionError = false;
        var newStatus = !activity.IsCompleted;

        try
        {
            var result = await DayPlanningClient.SetActivityCompletionAsync(
                activity.Id, newStatus);
            
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
            _isUpdatingCompletion = false;
        }

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

        var activityId = _activity.Id;
        _isDeleting = true;
        _deleteError = null;

        try
        {
            var result = await DayPlanningClient.DeletePlannedActivityAsync(activityId);
            
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
            _isDeleting = false;
        }

        _deleteError = "Verwijderen is niet gelukt. Probeer het opnieuw.";
    }

    private void OpenEdit() =>
        Navigation.NavigateTo($"/dayplanning/activities/{ActivityId}/edit?date={_date:yyyy-MM-dd}");

    private void ReturnToDay() =>
        Navigation.NavigateTo(DayUrl(_date), replace: true);

    private static string DayUrl(DateOnly date) =>
        $"/dayplanning?date={date:yyyy-MM-dd}";
}
