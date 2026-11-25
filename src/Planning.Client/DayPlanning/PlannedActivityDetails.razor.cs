using System.Globalization;
using Microsoft.AspNetCore.Components;
using Planning.Shared.DayPlanning;

namespace Planning.Client.DayPlanning;

public partial class PlannedActivityDetails
{
    [Parameter, EditorRequired]
    public PlannedActivityDto Activity { get; set; } = default!;

    [Parameter, EditorRequired]
    public DateOnly Date { get; set; }

    [Parameter, EditorRequired]
    public TimeBlockDto TimeBlock { get; set; } = default!;

    [Parameter]
    public bool CompletionError { get; set; }

    [Parameter]
    public bool CompletionUpdating { get; set; }

    [Parameter]
    public bool IsPage { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter]
    public EventCallback OnEdit { get; set; }

    [Parameter]
    public EventCallback OnDelete { get; set; }

    [Parameter]
    public EventCallback<PlannedActivityDto> OnToggleCompletion { get; set; }

    private string DateLabel
    {
        get
        {
            var culture = CultureInfo.GetCultureInfo("nl-BE"); // kiest Belgisch-Nederlandse cultuur
            var text = Date.ToString("dddd d MMMM yyyy", culture); // maakt "dinsdag 24 november 2026"
            return $"{char.ToUpper(text[0], culture)}{text[1..]}";
        }
    }
}
