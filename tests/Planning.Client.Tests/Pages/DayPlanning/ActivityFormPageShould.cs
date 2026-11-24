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
        service.GetDayAsync(originalDate, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = originalDate,
                    TimeBlocks = [morning]
                })));
        service.UpdateActivityAsync(
                101,
                Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.NoContent)));
        Services.AddSingleton(service);

        var cut = Render<ActivityFormPage>(parameters => parameters
            .Add(page => page.ActivityId, 101)
            .Add(page => page.DateQuery, "2026-11-24"));

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
}
