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
    private bool IsToday => _selectedDate == DateOnly.FromDateTime(DateTime.Today);
    
    [Inject] public required IDayPlanningService DayPlanningService { get; set; }

    protected override Task OnInitializedAsync() => LoadDayAsync(_selectedDate);

    private Task ShowPreviousDayAsync() =>
        LoadDayAsync(_selectedDate.AddDays(-1));

    private Task ShowNextDayAsync() =>
        LoadDayAsync(_selectedDate.AddDays(1));

    private Task ShowTodayAsync() =>
        LoadDayAsync(DateOnly.FromDateTime(DateTime.Today));

    private async Task LoadDayAsync(DateOnly date)
    {
        _selectedDate = date;
        _isLoading = true;
        _hasLoadError = false;
        _timeBlocks = [];

        try
        {
            var result = await DayPlanningService.GetDayAsync(date);

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
            Log.Warning(ex, "Could not load day planning for {Date}", date);
            _hasLoadError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }
}
