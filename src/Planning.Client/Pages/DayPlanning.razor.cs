using System.Globalization;
using System.Net;
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
    private int _loadVersion;
    private readonly HashSet<int> _completionUpdatesInProgress = [];
    private int? _completionErrorActivityId;
    private bool _isActivityFormOpen;
    private bool _isActivityDetailsOpen;
    private PlannedActivityDto? _selectedActivity;
    private DateOnly? _selectedActivityDate;
    private string? _selectedTimeBlockName;
    private int? _initialTimeBlockId;
    private string? _activityError;
    private int _activityOpenVersion;
    private bool _showDeleteConfirmation;
    private readonly HashSet<int> _deletionsInProgress = [];
    private string? _deleteError;
    private bool _restoreFocusAfterCancel;
    private ElementReference _globalAddButton;
    private ElementReference _activityFormTrigger;
    
    [Inject] public required IDayPlanningClient DayPlanningService { get; set; }

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "date")] // leest /dayplanning?date=2026-11-24 uit => DateQuery wordt dan de string "2026-11-24".
    public string? DateQuery { get; set; }

    private string NewActivityUrl =>
        $"/dayplanning/activities/new?date={_selectedDate:yyyy-MM-dd}";

    private string DeleteConfirmationMessage =>
        $"“{_selectedActivity?.Name}” wordt verwijderd uit je dagplanning.";

    private bool SelectedActivityDeleting =>
        _selectedActivity is not null && _deletionsInProgress.Contains(_selectedActivity.Id);

    protected override Task OnParametersSetAsync()
    {
        var date = DateOnly.TryParseExact(
            DateQuery, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.Today); // als DateQuery ongeldig is of leeg -> vandaag zetten

        return _loadVersion == 0 || date != _selectedDate // _loadVersion == 0 wil zeggen: er is nog nooit geladen
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
        var loadVersion = ++_loadVersion; // verhoogt _loadVersion eerst met 1 en daarna nieuwe waarde opslaan in loadVersion
        _selectedDate = date;
        _isLoading = true;
        _hasLoadError = false;
        _timeBlocks = [];
        _completionErrorActivityId = null;
        _activityError = null;
        _isActivityDetailsOpen = false;

        try
        {
            var result = await DayPlanningService.GetDayAsync(date);

            if (loadVersion != _loadVersion)
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
            if (loadVersion == _loadVersion)
            {
                Log.Warning(ex, "Could not load day planning for {Date}", date);
                _hasLoadError = true;
            }
        }
        finally
        {
            if (loadVersion == _loadVersion)
                _isLoading = false;
        }
    }

    private async Task SelectDateAsync(DateOnly date)
    {
        var requestedLoadVersion = _loadVersion + 1;
        await LoadDayAsync(date);
        if (requestedLoadVersion != _loadVersion)
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

                var dayActivity = _timeBlocks
                    .SelectMany(block => block.Activities)
                    .FirstOrDefault(planned => planned.Id == activity.Id);
                if (dayActivity is not null)
                    dayActivity.IsCompleted = newStatus;

                if (_selectedActivity?.Id == activity.Id)
                    _selectedActivity.IsCompleted = newStatus;
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

    private void OpenActivityForm(int? timeBlockId, ElementReference trigger)
    {
        ++_activityOpenVersion;
        ClearSelectedActivity();
        _activityError = null;
        _activityFormTrigger = trigger;
        _initialTimeBlockId = timeBlockId;
        _isActivityFormOpen = true;
    }

    private async Task OpenExistingActivity(int activityId, ElementReference trigger)
    {
        var dayLoadVersion = _loadVersion;
        var activityOpenVersion = ++_activityOpenVersion;
        _activityError = null;

        try
        {
            var result = await DayPlanningService.GetActivityAsync(activityId);

            if (dayLoadVersion != _loadVersion || activityOpenVersion != _activityOpenVersion)
                return;

            if (!result.IsSuccess || result.Value is null)
            {
                _activityError = result.StatusCode == HttpStatusCode.NotFound
                    ? "Deze activiteit staat niet meer in de dagplanning."
                    : "De activiteit kon niet geladen worden.";
                return;
            }

            var details = result.Value;
            _selectedActivity = details.ToPlannedActivity();
            _selectedActivityDate = details.Date;
            _selectedTimeBlockName = details.TimeBlockName;
            _activityFormTrigger = trigger;
            _initialTimeBlockId = details.TimeBlockId;
            _isActivityDetailsOpen = true;
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            if (dayLoadVersion == _loadVersion && activityOpenVersion == _activityOpenVersion)
            {
                Log.Warning(ex, "Could not load activity details for {ActivityId}", activityId);
                _activityError = "De activiteit kon niet geladen worden.";
            }
        }
    }

    private void ClearSelectedActivity()
    {
        _selectedActivity = null;
        _selectedActivityDate = null;
        _selectedTimeBlockName = null;
        _initialTimeBlockId = null;
    }

    private void CloseActivityDetails()
    {
        _isActivityDetailsOpen = false;
        _showDeleteConfirmation = false;
        ClearSelectedActivity();
        _restoreFocusAfterCancel = true;
    }

    private void EditSelectedActivity()
    {
        ++_activityOpenVersion;
        _isActivityDetailsOpen = false;
        _isActivityFormOpen = true;
    }

    private void ShowDeleteConfirmation()
    {
        _deleteError = null;
        _activityError = null;
        _showDeleteConfirmation = true;
    }

    private void CancelDeleteConfirmation()
    {
        _showDeleteConfirmation = false;
        _deleteError = null;
    }

    private async Task DeleteSelectedActivityAsync()
    {
        if (_selectedActivity is null)
            return;

        var activityId = _selectedActivity.Id;
        var dayLoadVersion = _loadVersion;
        if (!_deletionsInProgress.Add(activityId))
            return;

        _deleteError = null;

        try
        {
            var result = await DayPlanningService.DeletePlannedActivityAsync(activityId);

            if (result.IsSuccess)
            {
                ApplyDeletedActivity(activityId);
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
            _deletionsInProgress.Remove(activityId);
        }

        if (_showDeleteConfirmation && _selectedActivity?.Id == activityId)
            _deleteError = "Verwijderen is niet gelukt. Probeer het opnieuw.";
        else if (dayLoadVersion == _loadVersion)
            _activityError = "Verwijderen is niet gelukt. Probeer het opnieuw.";
    }

    private void ApplyDeletedActivity(int activityId)
    {
        foreach (var block in _timeBlocks)
            block.Activities = block.Activities
                .Where(activity => activity.Id != activityId)
                .ToArray();

        if (_selectedActivity?.Id != activityId)
            return;

        _showDeleteConfirmation = false;
        _isActivityDetailsOpen = false;
        _isActivityFormOpen = false;
        ClearSelectedActivity();
        _activityFormTrigger = default;
        _restoreFocusAfterCancel = true;
    }

    private void CloseActivityForm()
    {
        _isActivityFormOpen = false;
        ClearSelectedActivity();
    }

    private void CancelActivityForm()
    {
        if (!_isActivityFormOpen)
            return;

        if (_selectedActivity is not null)
        {
            _isActivityFormOpen = false;
            _isActivityDetailsOpen = true;
            return;
        }

        CloseActivityForm();
        _restoreFocusAfterCancel = true;
    }

    private async Task ActivitySavedAsync(DateOnly date, int activityOpenVersion)
    {
        if (!_isActivityFormOpen || activityOpenVersion != _activityOpenVersion)
            return;

        var editedActivityId = _selectedActivity?.Id;
        CloseActivityForm();
        var expectedLoadVersion = _loadVersion + 1;
        await LoadDayAsync(date);
        if (expectedLoadVersion != _loadVersion ||
            activityOpenVersion != _activityOpenVersion)
            return;

        Navigation.NavigateTo($"/dayplanning?date={date:yyyy-MM-dd}", replace: true);

        if (editedActivityId is not null)
            await OpenExistingActivity(editedActivityId.Value, default);
    }
}
