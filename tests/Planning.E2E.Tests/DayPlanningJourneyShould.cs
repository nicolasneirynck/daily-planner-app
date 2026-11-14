using System.Globalization;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit;

namespace Planning.E2E.Tests;

public class DayPlanningJourneyShould : PageTest
{
    private const string BaseUrl = "https://localhost:5001";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true
    };

    [Fact]
    public async Task ShowPlanningAndNavigateBetweenDays()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);
        var culture = CultureInfo.GetCultureInfo("nl-BE");

        await Page.GotoAsync($"{BaseUrl}/login?returnUrl=%2Fdayplanning");
        await Page.GetByPlaceholder("Email").FillAsync("user@example.com");
        await Page.GetByPlaceholder("Password").FillAsync("A1b2C3!");

        // Act: open today
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

        // Assert
        await Expect(Page).ToHaveURLAsync($"{BaseUrl}/dayplanning");
        await Expect(Page.GetByTestId("selected-date"))
            .ToHaveTextAsync(today.ToString("dddd d MMMM", culture));
        await Expect(Page.GetByTestId("time-block-name")).ToHaveCountAsync(6);
        await Expect(Page.GetByTestId("planned-activity-name").First)
            .ToBeVisibleAsync();

        // Act: go to tomorrow
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("next-day-button").ClickAsync(),
            response => response.Url.EndsWith($"/api/day-planning/{tomorrow:yyyy-MM-dd}")
                        && response.Status == 200);

        // Assert
        await Expect(Page.GetByTestId("selected-date"))
            .ToHaveTextAsync(tomorrow.ToString("dddd d MMMM", culture));
        await Expect(Page.GetByTestId("time-block-name")).ToHaveCountAsync(6);

        // Act: return to today
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("today-button").ClickAsync(),
            response => response.Url.EndsWith($"/api/day-planning/{today:yyyy-MM-dd}")
                        && response.Status == 200);

        // Assert
        await Expect(Page.GetByTestId("selected-date"))
            .ToHaveTextAsync(today.ToString("dddd d MMMM", culture));
        await Expect(Page.GetByTestId("time-block-name")).ToHaveCountAsync(6);
    }
}
