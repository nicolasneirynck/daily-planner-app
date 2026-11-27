using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Planning.Client.DayPlanning;
using Planning.Shared.DayPlanning;
using Serilog;

namespace Planning.Client.Pages;

public partial class ActivityFormPage : IDisposable
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
    private bool _isDisposed;
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

        var requestedActivityId = ActivityId;
        var hasLinkDate = DateOnly.TryParseExact(DateQuery, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var linkDate);
        if (requestedActivityId is null && !hasLinkDate)
        {
            _date = DateOnly.FromDateTime(DateTime.Today);
            _loadError = "De datum in de link is ongeldig.";
            _isLoading = false;
            return;
        }

        _date = hasLinkDate ? linkDate : DateOnly.FromDateTime(DateTime.Today);
        var requestedTimeBlockId = TimeBlockId;

        try
        {
            if (requestedActivityId is not null)
            {
                var activityResult = await DayPlanningClient.GetActivityAsync(
                    requestedActivityId.Value);
                if (loadVersion != _loadVersion)
                    return;

                if (!activityResult.IsSuccess || activityResult.Value is null)
                {
                    _loadError = activityResult.StatusCode == HttpStatusCode.NotFound
                        ? "Deze activiteit staat niet meer in de dagplanning."
                        : "De activiteit kon niet geladen worden.";
                    return;
                }

                var details = activityResult.Value;
                _date = details.Date;
                _initialTimeBlockId = details.TimeBlockId;
                _activity = details.ToPlannedActivity();
            }

            var requestedDate = _date;
            var result = await DayPlanningClient.GetDayAsync(requestedDate);

            if (loadVersion != _loadVersion) // wanneer een nieuwe laadaanvraag start voordat een eerdere klaar is
                return;
            /*
             * Bijvoorbeeld:
               1. De aanvraag voor 24 november start met loadVersion = 1.
               2. Je navigeert snel naar 25 november. Die aanvraag start met versie 2.
               3. Het antwoord voor 24 november komt pas daarna terug. Voor dat antwoord geldt 1 != _loadVersion (nu 2), dus return voorkomt dat de oude dag opnieuw in beeld komt.

                OnParametersSetAsync loopt bij de eerste weergave en kan opnieuw lopen wanneer parameters veranderen,
                bijvoorbeeld wanneer de date-queryparameter in de URL verandert.
             */

            if (!result.IsSuccess || result.Value is null)
            {
                _loadError = "De dagplanning kon niet geladen worden.";
                return;
            }

            _timeBlocks = result.Value.TimeBlocks
                .OrderBy(block => block.SortOrder)
                .ToArray();

            if (requestedActivityId is null)
                _initialTimeBlockId = requestedTimeBlockId;

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
                Log.Warning(ex, "Could not load activity form for {Date}", _date);
                _loadError = requestedActivityId is not null && _activity is null
                    ? "De activiteit kon niet geladen worden."
                    : "De dagplanning kon niet geladen worden.";
            }
        }
        finally
        {
            if (loadVersion == _loadVersion)
                _isLoading = false;
        }
    }

    private void ReturnWithoutSaving() => ReturnToDetailsOrDay(_date);

    private void ReturnToDetailsOrDay(DateOnly date) =>
        Navigation.NavigateTo(
            ActivityId is int id ? DetailsUrl(id, date) : DayUrl(date),
            replace: true);

    private void ReturnAfterSave(DateOnly date, int formLoadVersion)
    {
        if (_isDisposed || formLoadVersion != _loadVersion)
            return;

        ReturnToDetailsOrDay(date);
    }

    private void ReturnToDayAfterDeletion(int deletedActivityId)
    {
        if (_isDisposed || ActivityId != deletedActivityId)
            return;

        Navigation.NavigateTo(DayUrl(_date), replace: true);
    }

    private static string DetailsUrl(int activityId, DateOnly date) =>
        $"/dayplanning/activities/{activityId}?date={date:yyyy-MM-dd}";

    private static string DayUrl(DateOnly date) =>
        $"/dayplanning?date={date:yyyy-MM-dd}";

    public void Dispose() => _isDisposed = true;
}
