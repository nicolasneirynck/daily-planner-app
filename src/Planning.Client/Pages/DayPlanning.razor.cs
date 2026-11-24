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
    private int _loadVersion;
    private readonly HashSet<int> _completionUpdatesInProgress = [];
    private int? _completionErrorActivityId;
    private bool _isActivityFormOpen;
    private PlannedActivityDto? _selectedActivity;
    private int? _initialTimeBlockId;
    private bool _restoreFocusAfterCancel;
    private ElementReference _globalAddButton;
    private ElementReference _activityFormTrigger;
    
    [Inject] public required IDayPlanningClient DayPlanningService { get; set; }

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "date")] // leest /dayplanning?date=2026-11-24 uit => DateQuery wordt dan de string "2026-11-24".
    public string? DateQuery { get; set; }

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
        await LoadDayAsync(date);
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

    private void OpenActivityForm(int? timeBlockId, ElementReference trigger)
    {
        _selectedActivity = null;
        _activityFormTrigger = trigger;
        _initialTimeBlockId = timeBlockId;
        _isActivityFormOpen = true;
    }

    private void OpenExistingActivity(PlannedActivityDto activity, ElementReference trigger, int timeBlockId)
    {
        _selectedActivity = activity;
        _activityFormTrigger = trigger;
        _initialTimeBlockId = timeBlockId;
        _isActivityFormOpen = true;
    }
    
    private void CloseActivityForm()
    {
        _isActivityFormOpen = false;
        _selectedActivity = null;
        _initialTimeBlockId = null;
    }

    private void CancelActivityForm()
    {
        if (!_isActivityFormOpen)
            return;

        CloseActivityForm();
        _restoreFocusAfterCancel = true;
    }

    private async Task ActivitySavedAsync(DateOnly date)
    {
        CloseActivityForm();
        await LoadDayAsync(date);
        Navigation.NavigateTo($"/dayplanning?date={date:yyyy-MM-dd}", replace: true);
    }

    private async Task ActivityDeletedAsync(DateOnly date)
    {
        CloseActivityForm();
        await LoadDayAsync(date);
    }
}
