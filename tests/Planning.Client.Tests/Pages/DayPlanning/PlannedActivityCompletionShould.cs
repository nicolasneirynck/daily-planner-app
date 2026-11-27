using System.Net;
using NSubstitute;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityCompletionShould : BunitContext
{
    public PlannedActivityCompletionShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
    }

    [Fact]
    public void MarkIncompleteActivityAsCompletedImmediately()
    {
        // Arrange
        var service = RegisterServiceWithActivity(isCompleted: false);
        service.SetActivityCompletionAsync(101, true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));

        var cut = Render<DayPlanning>();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe("false"));

        // Act
        cut.Find("[data-testid='activity-completion-toggle']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).SetActivityCompletionAsync(
                101, true, Arg.Any<CancellationToken>());
            cut.Find("[data-testid='activity-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe("true");
        });
    }

    [Fact]
    public void MarkCompletedActivityAsIncompleteImmediately()
    {
        // Arrange
        var service = RegisterServiceWithActivity(isCompleted: true);
        service.SetActivityCompletionAsync(101, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));

        var cut = Render<DayPlanning>();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe("true"));

        // Act
        cut.Find("[data-testid='activity-completion-toggle']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).SetActivityCompletionAsync(
                101, false, Arg.Any<CancellationToken>());
            cut.Find("[data-testid='activity-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe("false");
        });
    }

    [Fact]
    public void KeepActivityIncompleteAndShowErrorWhenCompletionFails()
    {
        // Arrange
        var service = RegisterServiceWithActivity(isCompleted: false);
        service.SetActivityCompletionAsync(101, true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(
                HttpStatusCode.UnprocessableEntity, "Save failed.")));

        var cut = Render<DayPlanning>();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe("false"));

        // Act
        cut.Find("[data-testid='activity-completion-toggle']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='activity-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe("false");
            cut.Find("[role='alert']").TextContent
                .ShouldContain("Status wijzigen is niet gelukt.");
        });
    }

    [Fact]
    public void CompleteActivityFromDetailsAndUpdateDayPlanning()
    {
        // Arrange
        var service = RegisterServiceWithActivity(isCompleted: false);
        ConfigureActivityDetails(service);
        service.SetActivityCompletionAsync(101, true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));

        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-detail-completion-toggle']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).SetActivityCompletionAsync(
                101, true, Arg.Any<CancellationToken>());
            cut.Find("[data-testid='activity-detail-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe("true");
            cut.Find("[data-testid='activity-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe("true");
        });
    }

    [Fact]
    public void KeepActivityIncompleteInDetailsWhenCompletionFails()
    {
        // Arrange
        var service = RegisterServiceWithActivity(isCompleted: false);
        ConfigureActivityDetails(service);
        service.SetActivityCompletionAsync(101, true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(
                HttpStatusCode.UnprocessableEntity, "Save failed.")));

        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-detail-completion-toggle']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).SetActivityCompletionAsync(
                101, true, Arg.Any<CancellationToken>());
            cut.Find("[data-testid='activity-detail-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe("false");
            cut.Find("[data-testid='activity-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe("false");
            cut.Find("[role='dialog'] [role='alert']").TextContent
                .ShouldContain("Status wijzigen is niet gelukt.");
        });
    }

    private IDayPlanningClient RegisterServiceWithActivity(bool isCompleted)
    {
        var morning = Block(1, "Morning", 6, 9);
        morning.Activities =
        [
            new PlannedActivityDto
            {
                Id = 101,
                Name = "Morning walk",
                IsCompleted = isCompleted
            }
        ];

        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [morning]
            })));
        Services.AddSingleton(service);
        return service;
    }

    private static void ConfigureActivityDetails(IDayPlanningClient service)
    {
        service.GetActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                HttpStatusCode.OK,
                new PlannedActivityDetailsDto
                {
                    Id = 101,
                    Name = "Morning walk",
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlockId = 1,
                    TimeBlockName = "Morning",
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(9, 0)
                })));
    }
}
