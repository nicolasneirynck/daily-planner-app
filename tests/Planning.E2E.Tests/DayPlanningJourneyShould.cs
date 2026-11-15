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
        var culture = CultureInfo.GetCultureInfo("nl-BE");

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
            .ToHaveTextAsync(today.ToString("dddd d MMMM", culture));
        await Expect(Page.GetByTestId("time-block-name")).ToHaveCountAsync(6);

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
