using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Serilog;

namespace Planning.Client.DayPlanning;

public partial class ActivityForm : IAsyncDisposable
{
    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    [Inject]
    private IDayPlanningClient DayPlanningClient { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Parameter, EditorRequired]
    public DateOnly InitialDate { get; set; }

    [Parameter]
    public int? InitialTimeBlockId { get; set; }

    [Parameter]
    public ActivityFormPresentation Presentation { get; set; } =
        ActivityFormPresentation.Modal;

    [Parameter]
    public PlannedActivityDto? ExistingActivity { get; set; }

    [Parameter, EditorRequired]
    public IReadOnlyList<TimeBlockDto> TimeBlocks { get; set; } = [];

    [Parameter, EditorRequired]
    public EventCallback OnClose { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<DateOnly> OnSaved { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<DateOnly> OnDeleted { get; set; }

    private ElementReference _activityDialog;
    private ElementReference _closeButton;
    private ElementReference _nameInput;
    private IJSObjectReference? _dialogModule;
    private DotNetObjectReference<ActivityForm>? _dialogCallback; // bewaart een verwijzing naar deze ActivityForm-instantie voor JavaScript. Na DotNetObjectReference.Create(this) kan JavaScript via die verwijzing een [JSInvokable]-methode op het formulier aanroepen. Het ? betekent dat het veld nog null is vóór de eerste render. Het veld wordt bewaard zodat de verwijzing later kan worden opgeruimd.
    private readonly ActivityFormModel _model = new();
    private EditContext _editContext = default!; // EditContext: houdt onder meer het formuliermodel, gewijzigde velden en validatiefouten bij.
                                                 // = default!: zegt tegen de C#-compiler: “Dit veld krijgt later een waarde.” De ! onderdrukt de waarschuwing dat het nu nog null is.
    private FieldIdentifier _nameField;
    private FieldIdentifier _descriptionField;
    private FieldIdentifier _timeBlockField;
    private ValidationMessageStore _serverMessages = default!; // een opslag voor validatiemeldingen van de server. Die is gekoppeld aan _editContext, zodat het formulier ze samen met Blazors gewone veldmeldingen kan tonen.
    private string? _saveError;
    private string? _deleteError;
    private bool _isDeleting;
    private bool _isAwaitingDeleteConfirmation;
    private ElementReference _deleteButton;
    private bool _focusDeleteButtonAfterRender;
    private bool _focusCloseButtonAfterRender;
    private bool _isSaving;
    private bool _isAwaitingCloseConfirmation;
    private bool _allowNavigation;
    private string? _pendingLocation;

    private bool HasUnsavedChanges =>
        ExistingActivity is not null &&
        (_model.Name.Trim() != ExistingActivity.Name.Trim() ||
         NormalizeDescription(_model.Description) !=
             NormalizeDescription(ExistingActivity.Description) ||
         _model.Date != InitialDate ||
         _model.TimeBlockId != InitialTimeBlockId ||
         _model.IsImportant != ExistingActivity.IsImportant);

    private bool HasChangedFormValues =>
        _model.Name != (ExistingActivity?.Name ?? string.Empty) ||
        (_model.Description ?? string.Empty) !=
            (ExistingActivity?.Description ?? string.Empty) ||
        _model.Date != InitialDate ||
        _model.TimeBlockId != InitialTimeBlockId ||
        _model.IsImportant != (ExistingActivity?.IsImportant ?? false);

    private static string? NormalizeDescription(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    protected override void OnInitialized()
    {
        _model.Name = ExistingActivity?.Name ?? string.Empty;
        _model.Description = ExistingActivity?.Description;
        _model.Date = InitialDate;
        _model.TimeBlockId = InitialTimeBlockId;
        _model.IsImportant = ExistingActivity?.IsImportant ?? false;
        _editContext = new EditContext(_model);
        _nameField = FieldIdentifier.Create(() => _model.Name); // Een FieldIdentifier bewaart welk model en welke eigenschap bedoeld worden. Daarmee kan de code later bijvoorbeeld de foutmeldingen van precies het naamveld opvragen of aan de EditContext melden dat TimeBlockId gewijzigd is.
        _descriptionField = FieldIdentifier.Create(() => _model.Description);
        _timeBlockField = FieldIdentifier.Create(() => _model.TimeBlockId);
        _serverMessages = new ValidationMessageStore(_editContext); // maakt een opslag voor serverfouten en koppelt die aan het formulier (_editContext)
        _editContext.OnValidationRequested += ClearServerMessagesOnSubmit; // registreert ClearServerMessagesOnSubmit als handler voor een validatieverzoek. Bij het indienen vraagt EditForm om validatie; de handler wist dan eerdere serverfouten uit die opslag.
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (Presentation == ActivityFormPresentation.Modal)
            {
                _dialogModule = await Js.InvokeAsync<IJSObjectReference>(
                    "import", "./js/activityDialog.js");

                // Roept onze JavaScript-functie aan, die de <dialog> (_activityDialog) via de browser als modal opent.
                _dialogCallback = DotNetObjectReference.Create(this); // Na DotNetObjectReference.Create(this) kan JavaScript via die verwijzing een [JSInvokable]-methode op het formulier aanroepen
                await _dialogModule.InvokeVoidAsync(
                    "showModal", _activityDialog, _dialogCallback);
            }

            if (ExistingActivity is null)
                await _nameInput.FocusAsync();
            else
                await _closeButton.FocusAsync();
        }

        if (_focusDeleteButtonAfterRender)
        {
            _focusDeleteButtonAfterRender = false;
            await _deleteButton.FocusAsync();
        }

        if (_focusCloseButtonAfterRender)
        {
            _focusCloseButtonAfterRender = false;
            await _closeButton.FocusAsync();
        }
    }

    private void ShowDeleteConfirmation() =>
        _isAwaitingDeleteConfirmation = true;

    private void CancelDeleteConfirmation()
    {
        _isAwaitingDeleteConfirmation = false;
        _focusDeleteButtonAfterRender = true;
    }

    private async Task RequestCloseAsync()
    {
        if (_isAwaitingCloseConfirmation)
            return;

        if (!HasChangedFormValues)
        {
            await OnClose.InvokeAsync();
            return;
        }

        _isAwaitingCloseConfirmation = true;
    }

    [JSInvokable] // maakt methode aanroepbaar vanuit JavaScript
    public Task RequestCloseFromBrowserAsync() => // RequestCloseFromBrowserAsync wordt vanuit JavaScript aangeroepen, buiten een Blazor UI-event. Na het zetten van _isAwaitingCloseConfirmation is een expliciete renderaanvraag nodig om de waarschuwing zichtbaar te maken.
        InvokeAsync(async () =>  // InvokeAsync(async () => { ... }) voert de code uit binnen Blazors componententext
        {
            var wasAwaitingConfirmation = _isAwaitingCloseConfirmation; // wasAwaitingConfirmation onthoudt of de waarschuwing al klaarstond
            await RequestCloseAsync(); // RequestCloseAsync() beslist: direct sluiten als er geen wijzigingen zijn, of _isAwaitingCloseConfirmation op true zetten.

            if (!wasAwaitingConfirmation && _isAwaitingCloseConfirmation)
                StateHasChanged();
        });

    private void ContinueEditing()
    {
        _isAwaitingCloseConfirmation = false;
        _pendingLocation = null;
        _focusCloseButtonAfterRender = true;
    }

    private async Task DiscardChangesAsync()
    {
        _allowNavigation = true;
        try
        {
            if (_pendingLocation is not null)
                Navigation.NavigateTo(_pendingLocation);
            else
                await OnClose.InvokeAsync();
        }
        catch
        {
            _allowNavigation = false;
            throw;
        }
    }

    private async Task BeforePageNavigationAsync(LocationChangingContext context)
    {
        if (_allowNavigation || !HasChangedFormValues)
            return;

        context.PreventNavigation();
        _pendingLocation = context.TargetLocation; // context.TargetLocation is de URL waar de gebruiker naartoe probeert te gaan
        await RequestCloseAsync();
        await InvokeAsync(StateHasChanged);
    }

    private string DeleteConfirmationMessage =>
        $"“{ExistingActivity!.Name}” wordt verwijderd uit je dagplanning.";

    private async Task SubmitAsync()
    {
        if (_isSaving || (ExistingActivity is not null && !HasUnsavedChanges))
            return;

        var timeBlockId = _model.TimeBlockId
            ?? throw new InvalidOperationException("A time block is required.");
        var name = _model.Name.Trim();
        var description = _model.Description?.Trim();
        _isSaving = true;
        _saveError = null;

        try
        {
            ClientResult result;
            
            if (ExistingActivity is not null)
            {
                result = await DayPlanningClient.UpdateActivityAsync(
                    ExistingActivity.Id, new UpdatePlannedActivityRequest
                    {
                        Date = _model.Date,
                        TimeBlockId = timeBlockId,
                        Name = name,
                        Description = NormalizeDescription(description),
                        IsImportant = _model.IsImportant
                    });
            }
            else
            {
                result = await DayPlanningClient.CreateActivityAsync(
                    new CreatePlannedActivityRequest
                    {
                        Date = _model.Date,
                        TimeBlockId = timeBlockId,
                        Name = name,
                        Description = description,
                        IsImportant = _model.IsImportant
                    });
            }

            if (result.IsSuccess)
            {
                _allowNavigation = true;
                await OnSaved.InvokeAsync(_model.Date); // het formulier meldt aan bovenliggend compontent dat het een succes is + geeft datum mee die nu in het formulier staat zodat bovenliggend component kan terugnavigeren naar de dagplanning van die datum en dag opnieuw kan laden (indien nodig)
                return;
            }

            ApplySaveErrors(result);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            _allowNavigation = false;
            Log.Warning(ex, "Could not save activity for {Date}", _model.Date);
            _saveError = "Opslaan is niet gelukt. Probeer het opnieuw.";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void ApplySaveErrors(ClientResult result)
    {
        var fieldErrors = result.FieldErrors
            .Where(error => error.Name is "name" or "description")
            .ToArray();

        _serverMessages.Clear();
        foreach (var error in fieldErrors.Where(error => error.Name == "name"))
            _serverMessages.Add(_nameField, error.Message);
        foreach (var error in fieldErrors.Where(error => error.Name == "description"))
            _serverMessages.Add(_descriptionField, error.Message);
        _editContext.NotifyValidationStateChanged();

        var hasOtherErrors = result.FieldErrors.Any(error =>
            error.Name is not ("name" or "description" or "$"));
        _saveError = fieldErrors.Length > 0 && !hasOtherErrors
            ? null
            : "Opslaan is niet gelukt. Probeer het opnieuw.";
    }

    private async Task DeleteAsync()
    {
        if (_isDeleting || ExistingActivity is null)
            return;

        var activityId = ExistingActivity.Id;
        _isDeleting = true;
        _deleteError = null;

        try
        {
            var result = await DayPlanningClient.DeletePlannedActivityAsync(activityId);
            if (!result.IsSuccess)
            {
                _deleteError = "Verwijderen is niet gelukt. Probeer het opnieuw.";
                return;
            }

            _allowNavigation = true;
            await OnDeleted.InvokeAsync(InitialDate);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or JsonException
                                   or InvalidOperationException)
        {
            _allowNavigation = false;
            Log.Warning(ex, "Could not delete activity {ActivityId}", activityId);
            _deleteError = "Verwijderen is niet gelukt. Probeer het opnieuw.";
        }
        finally
        {
            _isDeleting = false;
        }
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
        _dialogCallback?.Dispose();
        if (_dialogModule is not null)
            await _dialogModule.DisposeAsync(); // geeft de verwijzing naar die module weer vrij.
    }
}
