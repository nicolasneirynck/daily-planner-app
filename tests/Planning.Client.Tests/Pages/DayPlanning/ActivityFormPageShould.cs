using System.Net;
using AngleSharp.Html.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class ActivityFormPageShould : BunitContext
{
    public ActivityFormPageShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
    }

    [Fact]
    public void CreateActivityFromPageWithSelectedTimeBlockAndReturnToDay()
    {
        // Arrange
        var selectedDate = new DateOnly(2026, 11, 24);
        var morning = Block(1, "Ochtend", 6, 9);
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = call.ArgAt<DateOnly>(0),
                    TimeBlocks = [morning]
                })));
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));
        Services.AddSingleton(service);

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/dayplanning/activities/new?date=2026-11-24&timeBlockId=1");
        var cut = Render<ActivityFormPage>();

        cut.WaitForAssertion(() =>
        {
            ((IHtmlInputElement)cut.Find("[data-testid='activity-date']"))
                .Value.ShouldBe("2026-11-24");
            ((IHtmlSelectElement)cut.Find("[data-testid='activity-time-block']"))
                .Value.ShouldBe("1");
        });

        // Act
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).CreateActivityAsync(
                Arg.Is<CreatePlannedActivityRequest>(request =>
                    request.Date == selectedDate &&
                    request.TimeBlockId == 1 &&
                    request.Name == "Read a book"),
                Arg.Any<CancellationToken>());
            Services.GetRequiredService<NavigationManager>().Uri
                .ShouldEndWith("/dayplanning?date=2026-11-24");
        });
    }

    [Fact]
    public void SaveEditedActivityFromPageAndReturnToUpdatedDay()
    {
        // Arrange
        var originalDate = new DateOnly(2026, 11, 24);
        var updatedDate = originalDate.AddDays(1);
        var morning = Block(1, "Ochtend", 6, 9);
        morning.Activities =
        [
            new PlannedActivityDto
            {
                Id = 101,
                Name = "Morning walk"
            }
        ];

        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = call.ArgAt<DateOnly>(0),
                    TimeBlocks = [morning]
                })));
        service.UpdateActivityAsync(
                101,
                Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.NoContent)));
        Services.AddSingleton(service);

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/dayplanning/activities/101/edit?date=2026-11-24");
        var cut = Render<ActivityFormPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));

        cut.WaitForAssertion(() =>
            ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
                .Value.ShouldBe("Morning walk"));

        // Act
        cut.Find("[data-testid='activity-date']").Change("2026-11-25");
        cut.Find("form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).UpdateActivityAsync(
                101,
                Arg.Is<UpdatePlannedActivityRequest>(request =>
                    request.Date == updatedDate),
                Arg.Any<CancellationToken>());
            Services.GetRequiredService<NavigationManager>().Uri
                .ShouldEndWith("/dayplanning?date=2026-11-25");
        });
    }

    [Fact]
    public void KeepPageEditsUntilUserDiscardsThem()
    {
        // Arrange
        var originalDate = new DateOnly(2026, 11, 24);
        var morning = Block(1, "Ochtend", 6, 9);
        morning.Activities =
        [
            new PlannedActivityDto
            {
                Id = 101,
                Name = "Morning walk"
            }
        ];

        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(originalDate, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = originalDate,
                    TimeBlocks = [morning]
                })));
        Services.AddSingleton(service);

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/dayplanning/activities/101/edit?date=2026-11-24");
        var cut = Render<ActivityFormPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));

        cut.WaitForAssertion(() =>
            ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
                .Value.ShouldBe("Morning walk"));

        // Act
        cut.Find("[data-testid='activity-name']").Change("Evening walk");
        cut.Find("button[aria-label='Terug']").Click();

        // Assert
        cut.Find("[role='alertdialog']").TextContent
            .ShouldContain("Niet-opgeslagen wijzigingen");

        // Act
        cut.Find("[data-testid='continue-editing-button']").Click();

        // Assert
        cut.FindAll("[role='alertdialog']").ShouldBeEmpty();
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Evening walk");

        // Act
        cut.Find("button[aria-label='Terug']").Click();
        cut.Find("[data-testid='discard-changes-button']").Click();

        // Assert
        Services.GetRequiredService<NavigationManager>().Uri
            .ShouldEndWith("/dayplanning/activities/101?date=2026-11-24");
        _ = service.DidNotReceive().UpdateActivityAsync(
            Arg.Any<int>(),
            Arg.Any<UpdatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());
    }
}
