using Microsoft.AspNetCore.Components;
using Planning.Shared.DayPlanning;

namespace Planning.Client.DayPlanning;

public partial class AddActivityForm
{
    [Parameter, EditorRequired]
    public DateOnly InitialDate { get; set; }

    [Parameter]
    public int? InitialTimeBlockId { get; set; }

    [Parameter, EditorRequired]
    public IReadOnlyList<TimeBlockDto> TimeBlocks { get; set; } = [];

    [Parameter, EditorRequired]
    public EventCallback OnClose { get; set; }

    private string _name = string.Empty;
    private DateOnly _date;
    private int? _timeBlockId;
    private string? _nameError;

    protected override void OnInitialized()
    {
        _date = InitialDate;
        _timeBlockId = InitialTimeBlockId;
    }

    private void ValidateName()
    {
        _nameError = string.IsNullOrWhiteSpace(_name)
            ? "Vul een naam in."
            : null;
    }

    private void HandleNameInput(ChangeEventArgs args)
    {
        if (_nameError is not null && !string.IsNullOrWhiteSpace(args.Value?.ToString()))
            _nameError = null;
    }
}
