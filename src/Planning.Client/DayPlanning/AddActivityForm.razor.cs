using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Planning.Shared.DayPlanning;

namespace Planning.Client.DayPlanning;

public partial class AddActivityForm : IAsyncDisposable
{
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired]
    public DateOnly InitialDate { get; set; }

    [Parameter]
    public int? InitialTimeBlockId { get; set; }

    [Parameter, EditorRequired]
    public IReadOnlyList<TimeBlockDto> TimeBlocks { get; set; } = [];

    [Parameter, EditorRequired]
    public EventCallback OnClose { get; set; }
    
    [Parameter, EditorRequired]
    public EventCallback<CreatePlannedActivityRequest> OnSubmit { get; set; }
    
    [Parameter]
    public string? SaveError { get; set; }

    private ElementReference _dialog;
    private ElementReference _nameInput;
    private IJSObjectReference? _dialogModule;
    private string _name = string.Empty;
    private DateOnly _date;
    private int? _timeBlockId;
    private string? _nameError;
    private string? _timeBlockError;

    protected override void OnInitialized()
    {
        _date = InitialDate;
        _timeBlockId = InitialTimeBlockId;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        _dialogModule = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./js/activityDialog.js");

        // Roept onze JavaScript-functie aan, die de <dialog> (_dialog) via de browser als modal opent.
        await _dialogModule.InvokeVoidAsync("showModal", _dialog);
        await _nameInput.FocusAsync();
    }

    private async Task SubmitAsync()
    {
        _nameError = string.IsNullOrWhiteSpace(_name)
            ? "Vul een naam in."
            : null;
        
        _timeBlockError = _timeBlockId is null
            ? "Kies een tijdsblok."
            : null;

        if (_nameError is not null || _timeBlockError is not null)
            return;
        
        await OnSubmit.InvokeAsync(new CreatePlannedActivityRequest
        {
            Date = _date,
            TimeBlockId = _timeBlockId!.Value,
            Name = _name.Trim()
        });
    }

    private void HandleNameInput(ChangeEventArgs args)
    {
        if (_nameError is not null && !string.IsNullOrWhiteSpace(args.Value?.ToString()))
            _nameError = null;
    }
    
    private void HandleTimeBlockChange()
    {
        if (_timeBlockId is not null)
            _timeBlockError = null;
    }

    /* opruimmethode van de component. Om onnodig geheugengebruik te voorkomen.
     Blazor roept DisposeAsync() aan wanneer de AddActivityForm-component uit de pagina verdwijnt
     Bijvoorbeeld nadat de gebruiker het formulier sluit. */
    public async ValueTask DisposeAsync()
    {
        if (_dialogModule is not null)
            await _dialogModule.DisposeAsync(); // geeft de verwijzing naar die module weer vrij.
    }
}
