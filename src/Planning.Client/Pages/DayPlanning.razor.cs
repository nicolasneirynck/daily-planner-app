using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Planning.Client.DayPlanning;
using Planning.Shared.DayPlanning;
using Serilog;

namespace Planning.Client.Pages;

public partial class DayPlanning
{
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private IReadOnlyList<TimeBlockDto> _timeBlocks = [];
    private bool _isLoading = true;
    private bool _hasLoadError;
    private int _dayLoadVersion;
    private readonly HashSet<int> _completionUpdatesInProgress = [];
    private int? _completionErrorActivityId;
    private bool _isActivityFormOpen;
    private PlannedActivityDetailsDto? _selectedActivityDetails;
    private bool _isActivityDetailsLoading;
    private string? _activityDetailsError;
    private bool _isUpdatingDetailsCompletion;
    private bool _detailsCompletionError;
    private bool _showDetailsDeleteConfirmation;
    private bool _isDeletingDetails;
    private string? _detailsDeleteError;
    private int _activityDetailsLoadVersion;
    private int? _initialTimeBlockId;
    private bool _restoreFocusAfterCancel;
    private ElementReference _globalAddButton;
    private ElementReference _activityFormTrigger;
    
    [Inject] public required IDayPlanningClient DayPlanningService { get; set; }

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "date")] // leest /dayplanning?date=2026-11-24 uit => DateQuery wordt dan de string "2026-11-24".
    public string? DateQuery { get; set; }

    private string NewActivityUrl =>
        $"/dayplanning/activities/new?date={_selectedDate:yyyy-MM-dd}";

    private string DetailsDeleteConfirmationMessage =>
        $"“{_selectedActivityDetails?.Name}” wordt verwijderd uit je dagplanning.";

    protected override Task OnParametersSetAsync()
    {
        var date = DateOnly.TryParseExact(
            DateQuery, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.Today); // als DateQuery ongeldig is of leeg -> vandaag zetten

        return _dayLoadVersion == 0 || date != _selectedDate // _dayLoadVersion == 0 wil zeggen: er is nog nooit geladen
            ? LoadDayAsync(date)
            : Task.CompletedTask; // er hoeft niets opnieuw geladen worden
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_restoreFocusAfterCancel)
            return;

        _restoreFocusAfterCancel = false;

        if (string.IsNullOrEmpty(_activityFormTrigger.Id))
            await _globalAddButton.FocusAsync();
        else
            await _activityFormTrigger.FocusAsync();
    }

    private static string FormatSelectedDate(DateOnly date)
    {
        var culture = CultureInfo.GetCultureInfo("nl-BE");
        var text = date.ToString("dddd d MMMM", culture);
        return $"{char.ToUpper(text[0], culture)}{text[1..]}"; // char.ToUpper kan van de cultuur afhangen. De letter i wordt bijvoorbeeld I volgens de meeste taalregels, maar İ in het Turks.
    }

    private async Task LoadDayAsync(DateOnly date)
    {
        var dayLoadVersion = ++_dayLoadVersion; // verhoogt _dayLoadVersion eerst met 1 en daarna nieuwe waarde opslaan in dayLoadVersion
        ClearActivityDetails();
        _selectedDate = date;
        _isLoading = true;
        _hasLoadError = false;
        _timeBlocks = [];
        _completionErrorActivityId = null;

        try
        {
            var result = await DayPlanningService.GetDayAsync(date);

            if (dayLoadVersion != _dayLoadVersion)
                return;

            if (result.IsSuccess && result.Value is not null)
            {
                _timeBlocks = result.Value.TimeBlocks
                    .OrderBy(block => block.SortOrder)
                    .ToArray();
            }
            else
            {
                _hasLoadError = true;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            if (dayLoadVersion == _dayLoadVersion)
            {
                Log.Warning(ex, "Could not load day planning for {Date}", date);
                _hasLoadError = true;
            }
        }
        finally
        {
            if (dayLoadVersion == _dayLoadVersion)
                _isLoading = false;
        }
    }

    private async Task SelectDateAsync(DateOnly date)
    {
        var requestedDayLoadVersion = _dayLoadVersion + 1;
        await LoadDayAsync(date);
        if (requestedDayLoadVersion != _dayLoadVersion)
            return;

        Navigation.NavigateTo($"/dayplanning?date={date:yyyy-MM-dd}");
    }

    private async Task ToggleActivityCompletionAsync(PlannedActivityDto activity)
    {
        if (!_completionUpdatesInProgress.Add(activity.Id)) // geeft true terug als ID nieuw is
            return;

        _completionErrorActivityId = null;
        var newStatus = !activity.IsCompleted;

        try
        {
            var result = await DayPlanningService.SetActivityCompletionAsync(activity.Id, newStatus);

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
        finally // na return in try en na catch
        {
            _completionUpdatesInProgress.Remove(activity.Id);
        }

        _completionErrorActivityId = activity.Id;
    }

    private async Task ToggleDetailsCompletionAsync()
    {
        if (_selectedActivityDetails is null || _isUpdatingDetailsCompletion)
            return;

        var details = _selectedActivityDetails;
        var detailsLoadVersion = _activityDetailsLoadVersion;
        var newStatus = !details.IsCompleted;
        _isUpdatingDetailsCompletion = true;
        _detailsCompletionError = false;

        try
        {
            var result = await DayPlanningService.SetActivityCompletionAsync(details.Id, newStatus);
            if (detailsLoadVersion != _activityDetailsLoadVersion)
                return;

            if (result.IsSuccess)
            {
                details.IsCompleted = newStatus;
                var dayActivity = _timeBlocks
                    .SelectMany(block => block.Activities)
                    .FirstOrDefault(activity => activity.Id == details.Id);
                if (dayActivity is not null)
                    dayActivity.IsCompleted = newStatus;
                return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            Log.Warning(ex, "Could not update completion for activity {ActivityId}", details.Id);
        }
        finally
        {
            if (detailsLoadVersion == _activityDetailsLoadVersion)
                _isUpdatingDetailsCompletion = false;
        }

        if (detailsLoadVersion == _activityDetailsLoadVersion)
            _detailsCompletionError = true;
    }

    private void OpenActivityForm(int? timeBlockId, ElementReference trigger)
    {
        ClearActivityDetails();
        _activityFormTrigger = trigger;
        _initialTimeBlockId = timeBlockId;
        _restoreFocusAfterCancel = false;
        _isActivityFormOpen = true;
    }

    private async Task OpenActivityDetailsAsync(int activityId, ElementReference trigger)
    {
        _activityFormTrigger = trigger;
        var detailsLoadVersion = ++_activityDetailsLoadVersion;
        _selectedActivityDetails = null;
        _isActivityDetailsLoading = true;
        _activityDetailsError = null;

        try
        {
            var result = await DayPlanningService.GetActivityAsync(activityId);
            if (detailsLoadVersion != _activityDetailsLoadVersion)
                return;

            if (result.IsSuccess && result.Value is not null)
                _selectedActivityDetails = result.Value;
            else
                _activityDetailsError = "De activiteit kon niet geladen worden.";
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            if (detailsLoadVersion == _activityDetailsLoadVersion)
            {
                Log.Warning(ex, "Could not load activity details for {ActivityId}", activityId);
                _activityDetailsError = "De activiteit kon niet geladen worden.";
            }
        }
        finally
        {
            if (detailsLoadVersion == _activityDetailsLoadVersion)
                _isActivityDetailsLoading = false;
        }
    }

    private void ClearActivityDetails()
    {
        _activityDetailsLoadVersion++;
        _selectedActivityDetails = null;
        _activityDetailsError = null;
        _isActivityDetailsLoading = false;
        _isUpdatingDetailsCompletion = false;
        _detailsCompletionError = false;
        _showDetailsDeleteConfirmation = false;
        _isDeletingDetails = false;
        _detailsDeleteError = null;
    }

    private void CloseActivityDetails()
    {
        ClearActivityDetails();
        _restoreFocusAfterCancel = true;
    }

    private void ShowDetailsDeleteConfirmation()
    {
        _detailsDeleteError = null;
        _showDetailsDeleteConfirmation = true;
    }

    private void CancelDetailsDeleteConfirmation()
    {
        if (_isDeletingDetails)
            return;

        _showDetailsDeleteConfirmation = false;
        _detailsDeleteError = null;
    }

    private async Task DeleteFromDetailsAsync()
    {
        if (_isDeletingDetails || _selectedActivityDetails is not { } details)
            return;

        var detailsLoadVersion = _activityDetailsLoadVersion;
        _isDeletingDetails = true;
        _detailsDeleteError = null;

        try
        {
            var result = await DayPlanningService.DeletePlannedActivityAsync(details.Id);
            if (detailsLoadVersion != _activityDetailsLoadVersion)
                return;

            if (result.IsSuccess)
            {
                await LoadDayAsync(details.Date);
                _activityFormTrigger = default;
                _restoreFocusAfterCancel = true;
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
            if (detailsLoadVersion == _activityDetailsLoadVersion)
                _isDeletingDetails = false;
        }

        if (detailsLoadVersion == _activityDetailsLoadVersion)
            _detailsDeleteError = "Verwijderen is niet gelukt. Probeer het opnieuw.";
    }

    private void OpenEditDialog()
    {
        if (_selectedActivityDetails is null)
            return;

        _initialTimeBlockId = _selectedActivityDetails.TimeBlockId;
        _restoreFocusAfterCancel = false;
        _isActivityFormOpen = true;
    }
    
    private void CloseActivityForm()
    {
        _isActivityFormOpen = false;
        _initialTimeBlockId = null;
    }

    private void CancelActivityForm()
    {
        if (!_isActivityFormOpen)
            return;

        CloseActivityForm();
        _restoreFocusAfterCancel = _selectedActivityDetails is null;
    }

    private async Task ActivitySavedAsync(DateOnly date)
    {
        var editedActivityId = _selectedActivityDetails?.Id;
        CloseActivityForm();
        await LoadDayAsync(date);
        Navigation.NavigateTo($"/dayplanning?date={date:yyyy-MM-dd}", replace: true);

        if (editedActivityId is not null)
            await OpenActivityDetailsAsync(editedActivityId.Value, default);
    }

    private async Task ActivityDeletedAsync(DateOnly date)
    {
        CloseActivityForm();
        await LoadDayAsync(date);
    }
}
