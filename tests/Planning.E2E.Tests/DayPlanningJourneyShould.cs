using System.Globalization;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit;

namespace Planning.E2E.Tests;

public class DayPlanningJourneyShould(E2ETestEnvironment environment)
    : PageTest, IClassFixture<E2ETestEnvironment>
{
    private string BaseUrl =>
        environment.BaseAddress.ToString().TrimEnd('/');

    [Fact]
    public async Task ShowPlanningAndNavigateBetweenDays()
    {
        // Arrange
        await DayPlanningTestData.EnsureTimeBlocksAsync(environment);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);

        // Act: open today
        await LogInToDayPlanningAsync();

        // Assert
        await Expect(Page).ToHaveURLAsync($"{BaseUrl}/dayplanning");
        await Expect(Page.GetByTestId("selected-date"))
            .ToHaveTextAsync(ExpectedDateLabel(today));
        await Expect(Page.GetByTestId("time-block-name")).ToHaveCountAsync(6);

        // Act: go to tomorrow
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("next-day-button").ClickAsync(),
            response => response.Url.EndsWith($"/api/day-planning/{tomorrow:yyyy-MM-dd}")
                        && response.Status == 200);

        // Assert
        await Expect(Page.GetByTestId("selected-date"))
            .ToHaveTextAsync(ExpectedDateLabel(tomorrow));
        await Expect(Page.GetByTestId("time-block-name")).ToHaveCountAsync(6);

        // Act: return to today
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("today-button").ClickAsync(),
            response => response.Url.EndsWith($"/api/day-planning/{today:yyyy-MM-dd}")
                        && response.Status == 200);

        // Assert
        await Expect(Page.GetByTestId("selected-date"))
            .ToHaveTextAsync(ExpectedDateLabel(today));
        await Expect(Page.GetByTestId("time-block-name")).ToHaveCountAsync(6);
    }

    [Fact]
    public async Task AddActivityWithDescriptionToTimeBlockAndKeepItAfterReload()
    {
        // Arrange
        await DayPlanningTestData.EnsureTimeBlocksAsync(environment);
        var activityName = $"Read a book {Guid.NewGuid():N}";
        var activityDescription = "Bring the library card";

        await LogInToDayPlanningAsync();

        /* AriaRole.Region betekent: zoek een benoemd gedeelte van de pagina.
           Name = "Ochtend" -> kiest het gedeelte met de toegankelijke naam “Ochtend”. 
           In de huidige dagplanning is dat gedeelte een <section> waarvan de naam aan de kop “Ochtend” is gekoppeld. 
           Zo voorkom je dat de test per ongeluk een knop in een ander tijdsblok gebruikt.*/
        var morning = Page.GetByRole(AriaRole.Region, new() { Name = "Ochtend" });

        // Act: add an activity to the selected time block
        await morning.GetByTestId("add-activity-button").ClickAsync();
        await Page.GetByTestId("activity-name").FillAsync(activityName);
        await Page.GetByTestId("activity-description").FillAsync(activityDescription);
        await Page.GetByTestId("submit-activity-button").ClickAsync();

        // Assert: it appears immediately in the selected block
        await Expect(morning.GetByText(activityName, new() { Exact = true })) // Exact = true vraagt om precies de tekst in activityName, zonder extra woorden.
            .ToBeVisibleAsync();

        // Act: reopen the day
        await Page.ReloadAsync();

        // Assert: it is still there
        await Expect(morning.GetByText(activityName, new() { Exact = true }))
            .ToBeVisibleAsync();

        // Act: open the saved activity
        var activity = morning.GetByTestId("planned-activity-row")
            .Filter(new() { HasText = activityName });
        await activity.GetByTestId("activity-open-button").ClickAsync();

        // Assert: the saved description is available in the read-only detail
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("activity-detail-name"))
            .ToHaveTextAsync(activityName);
        await Expect(Page.GetByTestId("activity-detail-description"))
            .ToHaveTextAsync(activityDescription);
        await Expect(Page.Locator(
                "[role='dialog'] input, [role='dialog'] textarea, [role='dialog'] select"))
            .ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AddAndEditActivityOnMobilePageWithoutLosingUnsavedChanges()
    {
        // Arrange
        await DayPlanningTestData.EnsureTimeBlocksAsync(environment);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var activityName = $"Morning walk {Guid.NewGuid():N}";
        var editedName = $"Long morning walk {Guid.NewGuid():N}";

        await Page.SetViewportSizeAsync(390, 844);
        await LogInToDayPlanningAsync();

        var morning = Page.GetByRole(AriaRole.Region, new() { Name = "Ochtend" });

        // Act: add through the mobile page
        await morning.GetByTestId("mobile-add-activity-link").ClickAsync();
        await Expect(Page.GetByTestId("activity-form-page")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("activity-form-page")
                .GetByRole(AriaRole.Heading, new() { Name = "Activiteit toevoegen" }))
            .ToBeFocusedAsync();
        await Expect(Page.GetByTestId("activity-time-block").Locator("option:checked"))
            .ToHaveTextAsync("Ochtend");
        await Page.GetByTestId("activity-name").FillAsync(activityName);
        await Page.GetByTestId("submit-activity-button").ClickAsync();

        // Assert: return to the selected day
        await Expect(Page).ToHaveURLAsync($"{BaseUrl}/dayplanning?date={today:yyyy-MM-dd}");
        var activity = morning.GetByTestId("planned-activity-row")
            .Filter(new() { HasText = activityName });
        await Expect(activity).ToBeVisibleAsync();

        // Act: open the mobile detail, then edit and use browser Back
        await activity.GetByTestId("mobile-activity-open-link").ClickAsync();
        await Expect(Page.GetByTestId("activity-detail-page")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("activity-detail-name"))
            .ToHaveTextAsync(activityName);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Bewerken" })
            .ClickAsync();
        await Expect(Page.GetByTestId("activity-form-page")).ToBeVisibleAsync();
        await Page.GetByTestId("activity-name").FillAsync(editedName);
        await Page.EvaluateAsync("history.back()");

        // Assert: keep the unsaved edit and finish saving it
        await Expect(Page.GetByRole(AriaRole.Alertdialog)).ToBeVisibleAsync();
        await Page.GetByTestId("continue-editing-button").ClickAsync();
        await Expect(Page.GetByTestId("activity-name")).ToHaveValueAsync(editedName);
        await Page.GetByTestId("update-activity-button").ClickAsync();

        await Expect(Page).ToHaveURLAsync($"{BaseUrl}/dayplanning?date={today:yyyy-MM-dd}");
        var editedActivity = morning.GetByTestId("planned-activity-row")
            .Filter(new() { HasText = editedName });
        await Expect(editedActivity).ToBeVisibleAsync();

        // Assert: the edit persists after reload
        await Page.ReloadAsync();
        await Expect(editedActivity).ToBeVisibleAsync();
    }

    [Fact]
    public async Task KeepEditedActivityAfterReload()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);
        var originalName = $"Morning walk {Guid.NewGuid():N}";
        var editedName = $"Evening walk {Guid.NewGuid():N}";
        const string editedDescription = "Bring a jacket";

        await DayPlanningTestData.SeedMorningActivityAsync(
            environment, originalName, today);
        await LogInToDayPlanningAsync();

        var morning = Page.GetByRole(AriaRole.Region, new() { Name = "Ochtend" });
        var activity = morning.GetByTestId("planned-activity-row")
            .Filter(new() { HasText = originalName });

        // Act
        await activity.GetByTestId("activity-open-button").ClickAsync();
        await Page.GetByTestId("activity-detail-edit-button").ClickAsync();
        await Page.GetByTestId("activity-name").FillAsync(editedName);
        await Page.GetByTestId("activity-description")
            .FillAsync(editedDescription);
        await Page.GetByTestId("activity-date")
            .FillAsync(tomorrow.ToString("yyyy-MM-dd"));
        await Page.GetByTestId("activity-time-block")
            .SelectOptionAsync(new SelectOptionValue { Label = "Middag" });
        await Page.GetByTestId("activity-important").CheckAsync();

        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("update-activity-button").ClickAsync(),
            response => response.Request.Method == "PUT"
                        && response.Url.Contains("/api/day-planning/activities/")
                        && response.Status == 204);

        // Assert: the update appears on its new day and in its new time block
        await Expect(Page.GetByTestId("selected-date"))
            .ToHaveTextAsync(ExpectedDateLabel(tomorrow));
        var midday = Page.GetByRole(AriaRole.Region, new() { Name = "Middag" });
        var editedActivity = midday.GetByTestId("planned-activity-row")
            .Filter(new() { HasText = editedName });
        await Expect(editedActivity).ToBeVisibleAsync();
        await Expect(editedActivity.GetByTestId("activity-important-flag"))
            .ToBeVisibleAsync();

        // Act: reload the activity's new day
        await Page.ReloadAsync();

        // Assert: the saved activity and its details remain
        await Expect(Page).ToHaveURLAsync(
            $"{BaseUrl}/dayplanning?date={tomorrow:yyyy-MM-dd}");
        await Expect(Page.GetByTestId("selected-date"))
            .ToHaveTextAsync(ExpectedDateLabel(tomorrow));
        await Expect(editedActivity).ToBeVisibleAsync();
        await Expect(editedActivity.GetByTestId("activity-important-flag"))
            .ToBeVisibleAsync();
        await editedActivity.GetByTestId("activity-open-button").ClickAsync();
        await Expect(Page.GetByTestId("activity-detail-name"))
            .ToHaveTextAsync(editedName);
        await Expect(Page.GetByTestId("activity-detail-description"))
            .ToHaveTextAsync(editedDescription);
        await Expect(Page.GetByTestId("activity-detail-date"))
            .ToContainTextAsync(
                tomorrow.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("nl-BE")));
        await Expect(Page.GetByTestId("activity-detail-time-block"))
            .ToHaveTextAsync("Middag");
        await Expect(Page.GetByTestId("activity-detail-importance"))
            .ToHaveTextAsync("Belangrijk");
    }

    [Fact]
    public async Task DeleteSelectedActivityOnlyAfterConfirmationAndKeepItDeletedAfterReload()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var selectedName = $"Morning walk {Guid.NewGuid():N}";
        var otherName = $"Read a book {Guid.NewGuid():N}";
        await DayPlanningTestData.SeedMorningActivityAsync(
            environment, selectedName, today);
        await DayPlanningTestData.SeedMorningActivityAsync(
            environment, otherName, today);
        await LogInToDayPlanningAsync();

        var morning = Page.GetByRole(AriaRole.Region, new() { Name = "Ochtend" });
        var selected = morning.GetByTestId("planned-activity-row")
            .Filter(new() { HasText = selectedName });
        var other = morning.GetByTestId("planned-activity-row")
            .Filter(new() { HasText = otherName });

        // Act: open the detail and cancel deletion
        await selected.GetByTestId("activity-open-button").ClickAsync();
        await Page.GetByTestId("activity-detail-delete-button").ClickAsync();

        // Assert: confirmation is shown before deletion
        await Expect(Page.GetByRole(AriaRole.Alertdialog)).ToBeVisibleAsync();
        await Page.GetByTestId("cancel-delete-button").ClickAsync();
        await Expect(Page.GetByTestId("activity-detail-name"))
            .ToHaveTextAsync(selectedName);
        await Expect(selected).ToBeVisibleAsync();

        // Act: confirm deletion
        await Page.GetByTestId("activity-detail-delete-button").ClickAsync();
        /* Begin vóór de klik naar de DELETE-response te luisteren, zodat ook een snel
         serverantwoord wordt opgevangen. De test wacht op dat antwoord voordat hij verdergaat. */
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("confirm-delete-button").ClickAsync(),
            response => response.Request.Method == "DELETE"
                        && response.Url.Contains("/api/day-planning/activities/")
                        && response.Status == 204);

        // Assert: only the selected activity disappeared
        await Expect(selected).ToHaveCountAsync(0);
        await Expect(other).ToBeVisibleAsync();

        // Act: reopen the planning
        await Page.ReloadAsync();

        // Assert: deletion persisted
        await Expect(selected).ToHaveCountAsync(0);
        await Expect(other).ToBeVisibleAsync();
    }

    [Fact]
    public async Task KeepActivityCompletionChangesAfterReload()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var activityName = $"Morning walk {Guid.NewGuid():N}";
        await DayPlanningTestData.SeedMorningActivityAsync(
            environment, activityName, today);
        await LogInToDayPlanningAsync();

        var morning = Page.GetByRole(AriaRole.Region, new() { Name = "Ochtend" });
        var row = morning.GetByTestId("planned-activity-row")
            .Filter(new() { HasText = activityName }); // houd alleen de rij over waarvan de tekst activityName bevat
        var toggle = row.GetByTestId("activity-completion-toggle");
        var name = row.GetByTestId("planned-activity-name");

        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", "false");

        // Act: complete
        await toggle.ClickAsync();

        // Assert: visible immediately and retained after reload
        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(name).ToHaveCSSAsync("text-decoration-line", "line-through");
        await Page.ReloadAsync();
        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(name).ToHaveCSSAsync("text-decoration-line", "line-through");

        // Act: reopen
        await toggle.ClickAsync();

        // Assert: visible immediately and retained after reload
        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(name).ToHaveCSSAsync("text-decoration-line", "none");
        await Page.ReloadAsync();
        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(name).ToHaveCSSAsync("text-decoration-line", "none");
    }

    [Fact]
    public async Task CloseActivityDialogWithEscapeAndRestoreTriggerFocus()
    {
        // Arrange
        await DayPlanningTestData.EnsureTimeBlocksAsync(environment);
        await LogInToDayPlanningAsync();

        var addButton = Page.GetByTestId("global-add-activity-button");
        await addButton.ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Activiteit toevoegen" });
        await Expect(dialog).ToHaveAttributeAsync("open", "");
        await Expect(dialog.GetByTestId("activity-name")).ToBeFocusedAsync();

        // Act
        await Page.Keyboard.PressAsync("Escape");

        // Assert
        await Expect(dialog).ToHaveCountAsync(0); // na escape-toets is het formulier gesloten en uit de pagina verdwenen
        await Expect(addButton).ToBeFocusedAsync();
    }

    private async Task LogInToDayPlanningAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/login?returnUrl=%2Fdayplanning");
        await Page.GetByPlaceholder("Email")
            .FillAsync(E2ETestEnvironment.UserEmail);
        await Page.GetByPlaceholder("Password")
            .FillAsync(E2ETestEnvironment.UserPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" })
            .ClickAsync();
    }

    private static string ExpectedDateLabel(DateOnly date)
    {
        var culture = CultureInfo.GetCultureInfo("nl-BE");
        var text = date.ToString("dddd d MMMM", culture);
        return $"{char.ToUpper(text[0], culture)}{text[1..]}";
    }
}
