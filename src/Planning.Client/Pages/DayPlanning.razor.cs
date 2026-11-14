using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Planning.Shared.DayPlanning;
using Serilog;

namespace Planning.Client.Pages;

public partial class DayPlanning
{
    private readonly DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private IReadOnlyList<TimeBlockDto> _timeBlocks = [];
    private bool _isLoading = true;
    private bool _hasLoadError;
    
    [Inject] public required IDayPlanningService DayPlanningService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await DayPlanningService.GetDayAsync(_selectedDate);

            if (result.IsSuccess)
                _timeBlocks = result.Value.TimeBlocks
                    .OrderBy(block => block.SortOrder)
                    .ToArray();

            else _hasLoadError = true;
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            Log.Warning(ex, "Could not load day planning for {Date}", _selectedDate);
            _hasLoadError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }
}
