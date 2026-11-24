using System.Globalization;
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

    [SupplyParameterFromQuery(Name = "date")]
    public string? DateQuery { get; set; }

    [SupplyParameterFromQuery(Name = "timeBlockId")]
    public int? TimeBlockId { get; set; }

    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    private IReadOnlyList<TimeBlockDto> _timeBlocks = [];
    private bool _isLoading = true;
    private string? _loadError;
    private int _loadVersion;
    private string FormKey => $"{_date:yyyy-MM-dd}:{TimeBlockId}";

    protected override async Task OnParametersSetAsync()
    {
        var loadVersion = ++_loadVersion;
        _isLoading = true;
        _loadError = null;
        _timeBlocks = [];

        if (!DateOnly.TryParseExact(DateQuery, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _date))
        {
            _date = DateOnly.FromDateTime(DateTime.Today);
            _loadError = "De datum in de link is ongeldig.";
            _isLoading = false;
            return;
        }

        var requestedDate = _date;
        var requestedTimeBlockId = TimeBlockId;
        
        try
        {
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

            if (requestedTimeBlockId is not null &&
                _timeBlocks.All(block => block.Id != requestedTimeBlockId))
                _loadError = "Het tijdsblok bestaat niet meer.";
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            if (loadVersion == _loadVersion)
            {
                Log.Warning(ex, "Could not load activity form for {Date}", requestedDate);
                _loadError = "De dagplanning kon niet geladen worden.";
            }
        }
        finally
        {
            if (loadVersion == _loadVersion)
                _isLoading = false;
        }
    }

    private void ReturnToOriginalDay() => ReturnToDay(_date);

    private void ReturnToDay(DateOnly date) =>
        Navigation.NavigateTo(DayUrl(date), replace: true);

    private static string DayUrl(DateOnly date) =>
        $"/dayplanning?date={date:yyyy-MM-dd}";
}
