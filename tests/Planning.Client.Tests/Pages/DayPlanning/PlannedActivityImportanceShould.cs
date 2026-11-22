using System.Net;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using AngleSharp.Html.Dom;
using NSubstitute;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityImportanceShould : BunitContext
{
    public PlannedActivityImportanceShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
    }

    [Fact]
    public void DefaultToNotImportantWhenAddingActivity()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlocks = [Block(1, "Morning", 6, 9)]
                })));
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        var important = (IHtmlInputElement)cut.Find(
            "[data-testid='activity-important']");
        important.IsChecked.ShouldBeFalse();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).CreateActivityAsync(
                Arg.Is<CreatePlannedActivityRequest>(request => !request.IsImportant),
                Arg.Any<CancellationToken>());
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PassFinalImportanceSelectionToService(bool isImportant)
    {
        // Arrange
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlocks = [Block(1, "Morning", 6, 9)]
                })));
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='activity-important']").Change(true);
        if (!isImportant)
            cut.Find("[data-testid='activity-important']").Change(false);
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).CreateActivityAsync(
                Arg.Is<CreatePlannedActivityRequest>(request =>
                    request.IsImportant == isImportant),
                Arg.Any<CancellationToken>());
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShowReadOnlyImportanceWhenOpeningActivity(bool isImportant)
    {
        // Arrange
        var morning = Block(1, "Morning", 6, 9);
        morning.Activities =
        [
            new PlannedActivityDto
            {
                Id = 101,
                Name = "Morning walk",
                IsImportant = isImportant
            }
        ];

        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlocks = [morning]
                })));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();
        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='activity-open-button']")
                .Count.ShouldBe(1));

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();

        // Assert
        var dialog = cut.Find("[role='dialog']");
        var readOnlyIndicators = dialog.QuerySelectorAll(
            "[data-testid='activity-important-readonly']");
        readOnlyIndicators.Length.ShouldBe(isImportant ? 1 : 0);
        if (isImportant)
        {
            readOnlyIndicators[0].TextContent.ShouldContain("Belangrijk");
            readOnlyIndicators[0].QuerySelectorAll("input, button, a")
                .ShouldBeEmpty();
        }
        dialog.QuerySelectorAll("[data-testid='activity-important']")
            .ShouldBeEmpty();
    }

    [Fact]
    public void ShowImportantFlagAfterReopeningDay()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var date = call.ArgAt<DateOnly>(0);
                var morning = Block(1, "Morning", 6, 9);
                if (date == today)
                {
                    morning.Activities =
                    [
                        new PlannedActivityDto
                        {
                            Id = 101,
                            Name = "Morning walk",
                            IsImportant = true
                        }
                    ];
                }

                return Task.FromResult(new ClientResult<DayPlanningResponse>(
                    HttpStatusCode.OK,
                    new DayPlanningResponse
                    {
                        Date = date,
                        TimeBlocks = [morning]
                    }));
            });
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();
        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='activity-important-flag']")
                .Count.ShouldBe(1));

        // Act
        cut.Find("[data-testid='next-day-button']").Click();
        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='activity-important-flag']")
                .ShouldBeEmpty());
        cut.Find("[data-testid='previous-day-button']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='activity-important-flag']")
                .Count.ShouldBe(1);
            _ = service.Received(2).GetDayAsync(
                today, Arg.Any<CancellationToken>());
        });
    }

}
