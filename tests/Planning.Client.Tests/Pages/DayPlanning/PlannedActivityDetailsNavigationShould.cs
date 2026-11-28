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
}
