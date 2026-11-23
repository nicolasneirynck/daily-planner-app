using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
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
    private string? _activitySaveError;
    private IReadOnlyList<ClientFieldError> _activityFieldErrors = [];
    private bool _isDeletingActivity;
    private string? _activityDeleteError;
    private bool _restoreFocusAfterCancel;
    private ElementReference _globalAddButton;
    private ElementReference _activityFormTrigger;
    
    [Inject] public required IDayPlanningClient DayPlanningService { get; set; }

    protected override Task OnInitializedAsync() => LoadDayAsync(_selectedDate);

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
        _activitySaveError = null;
        _activityFieldErrors = [];
        _activityDeleteError = null;
    }

    private void CancelActivityForm()
    {
        if (!_isActivityFormOpen)
            return;

        CloseActivityForm();
        _restoreFocusAfterCancel = true;
    }

    private async Task AddActivityAsync(CreatePlannedActivityRequest request)
    {
        _activitySaveError = null;
        _activityFieldErrors = [];

        try
        {
            var result = await DayPlanningService.CreateActivityAsync(request);

            if (result.IsSuccess)
            {
                CloseActivityForm();
                await LoadDayAsync(request.Date);
                return;
            }

            ApplyActivitySaveErrors(result);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                       or JsonException
                                       or InvalidOperationException)
        {
            Log.Warning(ex, "Could not save activity for {Date}", request.Date);
            _activitySaveError = "Opslaan is niet gelukt. Probeer het opnieuw.";
        }
    }

    private async Task UpdateActivityAsync(UpdatePlannedActivityRequest request)
    {
        var activityId = _selectedActivity?.Id
            ?? throw new InvalidOperationException("No activity is selected.");

        _activitySaveError = null;
        _activityFieldErrors = [];

        try
        {
            var result = await DayPlanningService.UpdateActivityAsync(
                activityId, request);

            if (result.IsSuccess)
            {
                CloseActivityForm();
                await LoadDayAsync(request.Date);
                return;
            }

            ApplyActivitySaveErrors(result);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                       or JsonException
                                       or InvalidOperationException)
        {
            Log.Warning(ex, "Could not update activity {ActivityId}", activityId);
            _activitySaveError = "Opslaan is niet gelukt. Probeer het opnieuw.";
        }
    }

    private void ApplyActivitySaveErrors(ClientResult result)
    {
        var errors = result.FieldErrors.ToArray();
        _activityFieldErrors = errors
            .Where(error => error.Name is "name" or "description")
            .ToArray();

        var hasOtherErrors =
            errors.Any(error => error.Name is not ("name" or "description" or "$"));

        _activitySaveError = _activityFieldErrors.Count > 0 && !hasOtherErrors
            ? null
            : "Opslaan is niet gelukt. Probeer het opnieuw.";
    }

    private async Task DeleteActivityAsync(int activityId)
    {
        if (_isDeletingActivity)
            return;

        _isDeletingActivity = true;
        _activityDeleteError = null;

        try
        {
            var result = await DayPlanningService.DeletePlannedActivityAsync(activityId);

            if (!result.IsSuccess)
            {
                _activityDeleteError = "Verwijderen is niet gelukt. Probeer het opnieuw.";
                return;
            }

            CloseActivityForm();
            await LoadDayAsync(_selectedDate);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                       or JsonException
                                       or InvalidOperationException)
        {
            Log.Warning(ex, "Could not delete activity {ActivityId}", activityId);
            _activityDeleteError = "Verwijderen is niet gelukt. Probeer het opnieuw.";
        }
        finally
        {
            _isDeletingActivity = false;
        }
    }
}
