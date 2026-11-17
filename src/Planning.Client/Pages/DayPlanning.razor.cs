using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
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
    private bool _isActivityFormOpen;
    private int? _initialTimeBlockId;
    private string? _activitySaveError;
    private bool _restoreFocusAfterCancel;
    private ElementReference _globalAddButton;
    private ElementReference _activityFormTrigger;
    
    [Inject] public required IDayPlanningService DayPlanningService { get; set; }

    protected override Task OnInitializedAsync() => LoadDayAsync(_selectedDate);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_restoreFocusAfterCancel)
            return;

        _restoreFocusAfterCancel = false;
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

        try
        {
            var result = await DayPlanningService.GetDayAsync(date);

            if (loadVersion != _loadVersion)
                return;

            if (result.IsSuccess)
                _timeBlocks = result.Value.TimeBlocks
                    .OrderBy(block => block.SortOrder)
                    .ToArray();
            else
                _hasLoadError = true;
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

    private void OpenActivityForm(int? timeBlockId, ElementReference trigger)
    {
        _activityFormTrigger = trigger;
        _initialTimeBlockId = timeBlockId;
        _isActivityFormOpen = true;
    }
    
    private void CloseActivityForm()
    {
        _isActivityFormOpen = false;
        _initialTimeBlockId = null;
        _activitySaveError = null;
    }

    private void CancelActivityForm()
    {
        CloseActivityForm();
        _restoreFocusAfterCancel = true;
    }

    private async Task AddActivityAsync(CreatePlannedActivityRequest request)
    {
        _activitySaveError = null;
        bool saved;

        try
        {
            saved = (await DayPlanningService.CreateActivityAsync(request)).IsSuccess; 
            /* De haakjes zorgen ervoor dat C# eerst de async methode afwacht en daarna IsSuccess leest van het teruggegeven Result..
             is eigenlijk hetzelfde als: 
                var result = await DayPlanningService.CreateActivityAsync(request); 
                saved = result.IsSucces; 
            */
                                                                                       
        }
        catch (Exception ex) when (ex is HttpRequestException
                                       or JsonException
                                       or InvalidOperationException)
        {
            Log.Warning(ex, "Could not save activity for {Date}", request.Date);
            saved = false;
        }

        if (!saved)
        {
            _activitySaveError = "Opslaan is niet gelukt. Probeer het opnieuw.";
            return;
        }
        
        CloseActivityForm();
        await LoadDayAsync(request.Date);
    }
}
