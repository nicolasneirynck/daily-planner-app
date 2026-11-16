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

        await Page.GotoAsync($"{BaseUrl}/login?returnUrl=%2Fdayplanning");
        await Page.GetByPlaceholder("Email")
            .FillAsync(E2ETestEnvironment.UserEmail);
        await Page.GetByPlaceholder("Password")
            .FillAsync(E2ETestEnvironment.UserPassword);

        // Act: open today
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

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
    public async Task AddActivityToTimeBlockAndKeepItAfterReload()
    {
        // Arrange
        await DayPlanningTestData.EnsureTimeBlocksAsync(environment);
        var activityName = $"Read a book {Guid.NewGuid():N}";

        await Page.GotoAsync($"{BaseUrl}/login?returnUrl=%2Fdayplanning");
        await Page.GetByPlaceholder("Email")
            .FillAsync(E2ETestEnvironment.UserEmail);
        await Page.GetByPlaceholder("Password")
            .FillAsync(E2ETestEnvironment.UserPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" })
            .ClickAsync();

        /* AriaRole.Region betekent: zoek een benoemd gedeelte van de pagina.
           Name = "Ochtend" -> kiest het gedeelte met de toegankelijke naam “Ochtend”. 
           In de huidige dagplanning is dat gedeelte een <section> waarvan de naam aan de kop “Ochtend” is gekoppeld. 
           Zo voorkom je dat de test per ongeluk een knop in een ander tijdsblok gebruikt.*/
        var morning = Page.GetByRole(AriaRole.Region, new() { Name = "Ochtend" });

        // Act: add an activity to the selected time block
        await morning.GetByTestId("add-activity-button").ClickAsync();
        await Page.GetByTestId("activity-name").FillAsync(activityName);
        await Page.GetByTestId("submit-activity-button").ClickAsync();

        // Assert: it appears immediately in the selected block
        await Expect(morning.GetByText(activityName, new() { Exact = true })) // Exact = true vraagt om precies de tekst in activityName, zonder extra woorden.
            .ToBeVisibleAsync();

        // Act: reopen the day
        await Page.ReloadAsync();

        // Assert: it is still there
        await Expect(morning.GetByText(activityName, new() { Exact = true }))
            .ToBeVisibleAsync();
    }

    private static string ExpectedDateLabel(DateOnly date)
    {
        var culture = CultureInfo.GetCultureInfo("nl-BE");
        var text = date.ToString("dddd d MMMM", culture);
        return $"{char.ToUpper(text[0], culture)}{text[1..]}";
    }
}
