using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;

namespace Planning.Client.Pages;

public class ActivityDetailsPageShould : BunitContext
{
    [Fact]
    public void LoadActivityByIdAndShowItsStoredDetails()
    {
        // Arrange
        var client = RegisterActivityClient();

        // Act
        var cut = Render<ActivityDetailsPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='activity-detail-name']")
                .TextContent.Trim().ShouldBe("Morning walk");
            cut.Find("[data-testid='activity-detail-date']")
                .TextContent.Trim().ShouldBe("Dinsdag 24 november 2026");
            cut.Find("[data-testid='activity-detail-time-block']")
                .TextContent.Trim().ShouldBe("Ochtend");
            _ = client.Received(1).GetActivityAsync(
                101, Arg.Any<CancellationToken>());
            _ = client.DidNotReceive().GetDayAsync(
                Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void ReturnToStoredDayFromDetailsPage()
    {
        // Arrange
        RegisterActivityClient();

        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/dayplanning/activities/101?date=2026-11-23");

        var cut = Render<ActivityDetailsPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));

        // Act
        cut.WaitForAssertion(() =>
            cut.Find("button[aria-label='Terug naar dagplanning']")
                .ShouldNotBeNull());
        cut.Find("button[aria-label='Terug naar dagplanning']").Click();

        // Assert
        navigation.Uri.ShouldEndWith("/dayplanning?date=2026-11-24");
    }

    [Fact]
    public void OpenEditPageUsingStoredActivityDate()
    {
        // Arrange
        RegisterActivityClient();
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/dayplanning/activities/101?date=2026-11-23");
        var cut = Render<ActivityDetailsPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));

        // Act
        cut.WaitForAssertion(() =>
            cut.Find("button[aria-label='Bewerken']").ShouldNotBeNull());
        cut.Find("button[aria-label='Bewerken']").Click();

        // Assert
        navigation.Uri.ShouldEndWith(
            "/dayplanning/activities/101/edit?date=2026-11-24");
    }

    [Fact]
    public void ToggleCompletionByClickingTheWholeDetailsButton()
    {
        // Arrange
        var client = RegisterActivityClient();
        client.SetActivityCompletionAsync(101, true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.NoContent)));
        var cut = Render<ActivityDetailsPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));

        // Act
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-detail-completion-toggle']")
                .ShouldNotBeNull());
        cut.Find("[data-testid='activity-detail-completion-toggle']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = client.Received(1).SetActivityCompletionAsync(
                101, true, Arg.Any<CancellationToken>());
            var button = cut.Find("[data-testid='activity-detail-completion-toggle']");
            button.GetAttribute("aria-pressed").ShouldBe("true");
            button.TextContent.ShouldContain("Activiteit voltooid");
        });
    }

    [Fact]
    public void DeleteActivityOnlyAfterConfirmation()
    {
        // Arrange
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
        var client = RegisterActivityClient();
        client.DeletePlannedActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.NoContent)));
        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = Render<ActivityDetailsPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));

        // Act
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-detail-delete-button']")
                .ShouldNotBeNull());
        cut.Find("[data-testid='activity-detail-delete-button']").Click();

        // Assert
        cut.Find("[role='alertdialog']").TextContent
            .ShouldContain("Verwijderen uit planning?");
        _ = client.DidNotReceive().DeletePlannedActivityAsync(
            101, Arg.Any<CancellationToken>());

        // Act
        cut.Find("[data-testid='confirm-delete-button']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = client.Received(1).DeletePlannedActivityAsync(
                101, Arg.Any<CancellationToken>());
            navigation.Uri.ShouldEndWith("/dayplanning?date=2026-11-24");
        });
    }

    private IDayPlanningClient RegisterActivityClient()
    {
        var client = Substitute.For<IDayPlanningClient>();
        client.GetActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                HttpStatusCode.OK,
                new PlannedActivityDetailsDto
                {
                    Id = 101,
                    Name = "Morning walk",
                    Date = new DateOnly(2026, 11, 24),
                    TimeBlockId = 1,
                    TimeBlockName = "Ochtend",
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(9, 0)
                })));
        Services.AddSingleton(client);
        return client;
    }
}
