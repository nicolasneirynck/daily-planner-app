using System.Net;
using NSubstitute;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityDetailsNavigationShould : BunitContext
{
    [Fact]
    public void LinkToActivityDetailsOnMobile()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var morning = Block(1, "Morning", 6, 9);
        morning.Activities = [new PlannedActivityDto { Id = 101, Name = "Morning walk" }];
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse { Date = today, TimeBlocks = [morning] })));
        Services.AddSingleton(service);

        // Act
        var cut = Render<DayPlanning>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='mobile-activity-open-link']")
                .GetAttribute("href")
                .ShouldBe($"/dayplanning/activities/101?date={today:yyyy-MM-dd}"));
    }

    [Fact]
    public void OpenSelectedActivityDetailsInDialogOnDesktop()
    {
        // Arrange
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var morning = Block(1, "Morning", 6, 9);
        morning.Activities = [new PlannedActivityDto { Id = 101, Name = "Morning walk" }];

        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse { Date = today, TimeBlocks = [morning] })));
        service.GetActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                HttpStatusCode.OK,
                new PlannedActivityDetailsDto
                {
                    Id = 101,
                    Name = "Morning walk",
                    Date = today,
                    TimeBlockId = 1,
                    TimeBlockName = "Morning",
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(9, 0)
                })));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-open-button']").ShouldNotBeNull());
        cut.Find("[data-testid='activity-open-button']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var dialog = cut.Find("dialog[aria-label='Activiteit bekijken']");
            dialog.TextContent.ShouldContain("Morning walk");
            _ = service.Received(1).GetActivityAsync(
                101, Arg.Any<CancellationToken>());
        });
    }
}
