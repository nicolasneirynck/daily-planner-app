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
    public void LoadActivityByIdAndUseItsStoredDate()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningClient>();
        service.GetActivityAsync(101, Arg.Any<CancellationToken>())
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
        Services.AddSingleton(service);
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/dayplanning/activities/101?date=2026-11-23");

        // Act
        var cut = Render<ActivityDetailsPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='activity-detail-date']")
                .TextContent.Trim().ShouldBe("Dinsdag 24 november 2026");
            _ = service.Received(1).GetActivityAsync(
                101, Arg.Any<CancellationToken>());
            _ = service.DidNotReceive().GetDayAsync(
                Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        });

        cut.Find("[data-testid='activity-detail-edit-button']").Click();
        Services.GetRequiredService<NavigationManager>().Uri
            .ShouldEndWith("/dayplanning/activities/101/edit?date=2026-11-24");
    }
}
