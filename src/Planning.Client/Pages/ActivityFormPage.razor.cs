using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Planning.Client.DayPlanning;
using Planning.Shared.DayPlanning;
using Serilog;

namespace Planning.Client.Pages;

public partial class ActivityFormPage
{
    [Inject] private IDayPlanningClient DayPlanningClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter]
    public int? ActivityId { get; set; }

    [SupplyParameterFromQuery(Name = "date")]
    public string? DateQuery { get; set; }

    [SupplyParameterFromQuery(Name = "timeBlockId")]
    public int? TimeBlockId { get; set; }

    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    private IReadOnlyList<TimeBlockDto> _timeBlocks = [];
    private PlannedActivityDto? _activity;
    private int? _initialTimeBlockId;
    private bool _isLoading = true;
    private string? _loadError;
    private int _loadVersion;
    private string FormKey =>
        $"{_date:yyyy-MM-dd}:{ActivityId}:{_initialTimeBlockId}";

    protected override async Task OnParametersSetAsync()
    {
        var loadVersion = ++_loadVersion;
        _isLoading = true;
        _loadError = null;
        _timeBlocks = [];
        _activity = null;
        _initialTimeBlockId = null;

        var hasLinkDate = DateOnly.TryParseExact(
            DateQuery, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var linkDate);
        _date = hasLinkDate
            ? linkDate
            : DateOnly.FromDateTime(DateTime.Today);

        var requestedActivityId = ActivityId;
        if (requestedActivityId is null && !hasLinkDate)
        {
            _loadError = "De datum in de link is ongeldig.";
            _isLoading = false;
            return;
        }

        try
        {
            if (requestedActivityId is not null)
            {
                var activityLoaded =
                    await LoadExistingActivityAsync(requestedActivityId.Value, loadVersion);
                if (!activityLoaded)
                    return;
            }
            else
            {
                _initialTimeBlockId = TimeBlockId;
            }

            var requestedDate = _date;
            var result = await DayPlanningClient.GetDayAsync(requestedDate);

            if (loadVersion != _loadVersion)
                return;

            if (!result.IsSuccess || result.Value is null)
            {
                _loadError = "De dagplanning kon niet geladen worden.";
                return;
            }

            _timeBlocks = result.Value.TimeBlocks
                .OrderBy(block => block.SortOrder)
                .ToArray();

            if (_initialTimeBlockId is not null &&
                _timeBlocks.All(block => block.Id != _initialTimeBlockId))
            {
                _loadError = "Het tijdsblok bestaat niet meer.";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            if (loadVersion == _loadVersion)
            {
                Log.Warning(ex, "Could not load activity form for {ActivityId} on {Date}",
                    requestedActivityId, _date);
                _loadError = "De activiteit of dagplanning kon niet geladen worden.";
            }
        }
        finally
        {
            if (loadVersion == _loadVersion)
                _isLoading = false;
        }
    }

    private async Task<bool> LoadExistingActivityAsync(int activityId, int loadVersion)
    {
        var result = await DayPlanningClient.GetActivityAsync(activityId);

        if (loadVersion != _loadVersion)
            return false;

        if (!result.IsSuccess || result.Value is null)
        {
            _loadError = result.StatusCode == HttpStatusCode.NotFound
                ? "Deze activiteit staat niet meer in de dagplanning."
                : "De activiteit kon niet geladen worden.";
            return false;
        }

        var details = result.Value;
        _date = details.Date;
        _initialTimeBlockId = details.TimeBlockId;
        _activity = new PlannedActivityDto
        {
            Id = details.Id,
            Name = details.Name,
            Description = details.Description,
            IsImportant = details.IsImportant,
            IsCompleted = details.IsCompleted
        };
        return true;
    }

    private void ReturnAfterSave(DateOnly savedDate) =>
        NavigateToDetailsOrDay(savedDate);

    private void ReturnAfterClose() => NavigateToDetailsOrDay(_date);

    private void NavigateToDetailsOrDay(DateOnly date)
    {
        var url = ActivityId is int activityId
            ? $"/dayplanning/activities/{activityId}?date={date:yyyy-MM-dd}"
            : DayUrl(date);

        Navigation.NavigateTo(url, replace: true);
    }

    private void ReturnToDay(DateOnly date) =>
        Navigation.NavigateTo(DayUrl(date), replace: true);

    private static string DayUrl(DateOnly date) =>
        $"/dayplanning?date={date:yyyy-MM-dd}";
}
