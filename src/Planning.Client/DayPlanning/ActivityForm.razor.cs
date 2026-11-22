using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Planning.Client.Http;
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
    public IReadOnlyList<ClientFieldError> ServerErrors { get; set; } = [];

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
    private FieldIdentifier _descriptionField;
    private FieldIdentifier _timeBlockField;
    private ValidationMessageStore _serverMessages = default!; // een opslag voor validatiemeldingen van de server. Die is gekoppeld aan _editContext, zodat het formulier ze samen met Blazors gewone veldmeldingen kan tonen.
    private IReadOnlyList<ClientFieldError>? _appliedServerErrors; // onthoudt welke lijst serverfouten al is verwerkt, zodat dezelfde lijst bij een volgende render niet opnieuw wordt toegevoegd.
    private bool _isConfirmingDeletion;
    private ElementReference _deleteButton;
    private ElementReference _confirmationDialog;
    private bool _openConfirmationAfterRender;
    private bool _focusDeleteButtonAfterRender;

    protected override void OnInitialized()
    {
        _model.Name = ExistingActivity?.Name ?? string.Empty;
        _model.Description = ExistingActivity?.Description;
        _model.Date = InitialDate;
        _model.TimeBlockId = InitialTimeBlockId;
        _editContext = new EditContext(_model);
        _nameField = FieldIdentifier.Create(() => _model.Name); // Een FieldIdentifier bewaart welk model en welke eigenschap bedoeld worden. Daarmee kan de code later bijvoorbeeld de foutmeldingen van precies het naamveld opvragen of aan de EditContext melden dat TimeBlockId gewijzigd is.
        _descriptionField = FieldIdentifier.Create(() => _model.Description);
        _timeBlockField = FieldIdentifier.Create(() => _model.TimeBlockId);
        _serverMessages = new ValidationMessageStore(_editContext); // maakt een opslag voor serverfouten en koppelt die aan het formulier (_editContext)
        _editContext.OnValidationRequested += ClearServerMessagesOnSubmit; // registreert ClearServerMessagesOnSubmit als handler voor een validatieverzoek. Bij het indienen vraagt EditForm om validatie; de handler wist dan eerdere serverfouten uit die opslag.
    }

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_appliedServerErrors, ServerErrors)) // controleert of _appliedServerErrors en ServerErrors naar dezelfde lijstinstantie verwijzen. Als dat zo is, zijn die serverfouten al verwerkt en stopt return de methode.
            return;

        _appliedServerErrors = ServerErrors;
        _serverMessages.Clear();
        foreach (var error in ServerErrors.Where(error => error.Name == "name"))
            _serverMessages.Add(_nameField, error.Message); // De foreach loopt door de serverfouten waarvan Name gelijk is aan "name". Voor elk van die fouten voegt Add de fouttekst toe aan het naamveld (_nameField) in Blazors validatieopslag.
        foreach (var error in ServerErrors.Where(error => error.Name == "description"))
            _serverMessages.Add(_descriptionField, error.Message);
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
            TimeBlockId = _model.TimeBlockId
                ?? throw new InvalidOperationException("A time block is required."),
            Name = _model.Name.Trim(),
            Description = _model.Description?.Trim(),
            IsImportant = _model.IsImportant
        });
    }

    /*  Deze methode wordt aangeroepen wanneer het formulier om validatie vraagt, bijvoorbeeld bij indienen.
        _serverMessages.Clear() wist dan de eerdere serverfouten uit de ValidationMessageStore.

        Dat is nodig omdat Blazor een formulier met een nog aanwezige serverfout anders als ongeldig ziet
        en de tweede opslagpoging niet zou starten. De clientvalidatie wordt bij die indiening opnieuw uitgevoerd; de invoerregels blijven dus gelden.
       */
    private void ClearServerMessagesOnSubmit(object? sender, ValidationRequestedEventArgs args) =>
        _serverMessages.Clear();

    private void HandleNameInput(ChangeEventArgs args)
    {
        _model.Name = args.Value?.ToString() ?? string.Empty; // De nieuwe tekst uit args.Value wordt in _model.Name gezet. Als er geen waarde is, wordt dat "".
        _serverMessages.Clear(_nameField);
        _editContext.NotifyFieldChanged(_nameField); // NotifyFieldChanged(_nameField) meldt aan de EditContext dat de naam is gewijzigd. De DataAnnotationsValidator controleert daardoor opnieuw de regels voor dit veld.
    }

    private void HandleDescriptionInput(ChangeEventArgs args)
    {
        _model.Description = args.Value?.ToString();
        _serverMessages.Clear(_descriptionField);
        _editContext.NotifyFieldChanged(_descriptionField);
    }

    private void HandleTimeBlockChange()
    {
        _editContext.NotifyFieldChanged(_timeBlockField);
    }

    /* opruimmethode van de component. Om onnodig geheugengebruik te voorkomen.
     Blazor roept DisposeAsync() aan wanneer de ActivityForm-component uit de pagina verdwijnt
     Bijvoorbeeld nadat de gebruiker het formulier sluit. */
    public async ValueTask DisposeAsync()
    {
        _editContext.OnValidationRequested -= ClearServerMessagesOnSubmit;
        if (_dialogModule is not null)
            await _dialogModule.DisposeAsync(); // geeft de verwijzing naar die module weer vrij.
    }
}
