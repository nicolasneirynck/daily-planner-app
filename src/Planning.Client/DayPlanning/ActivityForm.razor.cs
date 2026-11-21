using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
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
    public IReadOnlyList<ValidationError> ServerErrors { get; set; } = [];

    [Parameter]
    public string? DeleteError { get; set; }

    [Parameter]
    public bool IsDeleting { get; set; }

    private ElementReference _activityDialog;
    private ElementReference _closeButton;
    private ElementReference _nameInput;
    private IJSObjectReference? _dialogModule;
    private readonly ActivityFormModel _model = new();
    private EditContext _editContext = default!; // EditContext: houdt onder meer het formuliermodel, gewijzigde velden en validatiefouten bij.
                                                 // = default!: zegt tegen de C#-compiler: “Dit veld krijgt later een waarde.” De ! onderdrukt de waarschuwing dat het nu nog null is.
    private FieldIdentifier _nameField;
    private FieldIdentifier _timeBlockField;
    private string? _nameError;
    private IReadOnlyList<ValidationError>? _appliedServerErrors;
    private string? _timeBlockError;
    private bool _isConfirmingDeletion;
    private ElementReference _deleteButton;
    private ElementReference _confirmationDialog;
    private bool _openConfirmationAfterRender;
    private bool _focusDeleteButtonAfterRender;

    protected override void OnInitialized()
    {
        _model.Name = ExistingActivity?.Name ?? string.Empty;
        _model.Date = InitialDate;
        _model.TimeBlockId = InitialTimeBlockId;
        _editContext = new EditContext(_model);
        _nameField = FieldIdentifier.Create(() => _model.Name); // Een FieldIdentifier bewaart welk model en welke eigenschap bedoeld worden. Daarmee kan de code later bijvoorbeeld de foutmeldingen van precies het naamveld opvragen of aan de EditContext melden dat TimeBlockId gewijzigd is.
        _timeBlockField = FieldIdentifier.Create(() => _model.TimeBlockId);
    }

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_appliedServerErrors, ServerErrors))
            return;

        _appliedServerErrors = ServerErrors;
        var nameErrors = ServerErrors
            .Where(error => error.Identifier == "name")
            .Select(error => error.ErrorMessage)
            .ToArray();

        _nameError = nameErrors.Length > 0
            ? string.Join(" ", nameErrors)
            : null;
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
        await OnSubmit.InvokeAsync(new CreatePlannedActivityRequest
        {
            Date = _model.Date,
            TimeBlockId = _model.TimeBlockId.Value,
            Name = _model.Name.Trim()
        });
    }

    private void HandleInvalidSubmit()
    {
        _nameError = _editContext.GetValidationMessages(_nameField)
            .FirstOrDefault();
        _timeBlockError = _editContext.GetValidationMessages(_timeBlockField)
            .FirstOrDefault();
    }

    private void HandleNameInput(ChangeEventArgs args)
    {
        _model.Name = args.Value?.ToString() ?? string.Empty; // De nieuwe tekst uit args.Value wordt in _model.Name gezet. Als er geen waarde is, wordt dat "".
        _editContext.NotifyFieldChanged(_nameField); // NotifyFieldChanged(_nameField) meldt aan de EditContext dat de naam is gewijzigd. De DataAnnotationsValidator controleert daardoor opnieuw de regels voor dit veld.
        _nameError = _editContext.GetValidationMessages(_nameField)
            .FirstOrDefault(); // GetValidationMessages(_nameField).FirstOrDefault() haalt de eerste actuele naamfout op. Als er geen fout meer is, wordt _nameError null en verdwijnt de foutmelding.
    }

    private void HandleTimeBlockChange()
    {
        _editContext.NotifyFieldChanged(_timeBlockField);
        _timeBlockError = _editContext.GetValidationMessages(_timeBlockField)
            .FirstOrDefault();
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
