using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Planning.Shared.DayPlanning;

namespace Planning.Client.DayPlanning;

public partial class ActivityForm : IAsyncDisposable
{
    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    [Parameter, EditorRequired]
    public DateOnly InitialDate { get; set; }

    [Parameter]
    public int? InitialTimeBlockId { get; set; }

    [Parameter]
    public PlannedActivityDto? ExistingActivity { get; set; }

    [Parameter, EditorRequired]
    public IReadOnlyList<TimeBlockDto> TimeBlocks { get; set; } = [];

    [Parameter, EditorRequired]
    public EventCallback OnClose { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<CreatePlannedActivityRequest> OnSubmit { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<int> OnConfirmDelete { get; set; }

    [Parameter]
    public string? SaveError { get; set; }

    [Parameter]
    public string? DeleteError { get; set; }

    [Parameter]
    public bool IsDeleting { get; set; }

    private ElementReference _activityDialog;
    private ElementReference _closeButton;
    private ElementReference _nameInput;
    private IJSObjectReference? _dialogModule;
    private string _name = string.Empty;
    private DateOnly _date;
    private int? _timeBlockId;
    private string? _nameError;
    private string? _timeBlockError;
    private bool _isConfirmingDeletion;
    private ElementReference _deleteButton;
    private ElementReference _confirmationDialog;
    private bool _openConfirmationAfterRender;
    private bool _focusDeleteButtonAfterRender;

    protected override void OnInitialized()
    {
        _name = ExistingActivity?.Name ?? string.Empty;
        _date = InitialDate;
        _timeBlockId = InitialTimeBlockId;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dialogModule = await Js.InvokeAsync<IJSObjectReference>(
                "import", "./js/activityDialog.js");

            // Roept onze JavaScript-functie aan, die de <dialog> (_activityDialog) via de browser als modal opent.
            await _dialogModule.InvokeVoidAsync("showModal", _activityDialog);

            if (ExistingActivity is null)
                await _nameInput.FocusAsync();
            else
                await _closeButton.FocusAsync();
        }

        if (_openConfirmationAfterRender)
        {
            _openConfirmationAfterRender = false;
            await _dialogModule!.InvokeVoidAsync("showModal", _confirmationDialog);
        }

        if (_focusDeleteButtonAfterRender)
        {
            _focusDeleteButtonAfterRender = false;
            await _deleteButton.FocusAsync();
        }
    }

    private void ShowDeleteConfirmation()
    {
        _isConfirmingDeletion = true;
        _openConfirmationAfterRender = true;
    }

    private void CancelDeleteConfirmation()
    {
        _isConfirmingDeletion = false;
        _focusDeleteButtonAfterRender = true;
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
     Blazor roept DisposeAsync() aan wanneer de ActivityForm-component uit de pagina verdwijnt
     Bijvoorbeeld nadat de gebruiker het formulier sluit. */
    public async ValueTask DisposeAsync()
    {
        if (_dialogModule is not null)
            await _dialogModule.DisposeAsync(); // geeft de verwijzing naar die module weer vrij.
    }
}
