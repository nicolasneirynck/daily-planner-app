using Ardalis.Result;
using NSubstitute;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class DayPlanningCompletionShould : BunitContext
{
    [Fact]
    public void MarkIncompleteActivityAsCompletedImmediately()
    {
        // Arrange
        var service = RegisterServiceWithActivity(isCompleted: false);
        service.SetActivityCompletionAsync(101, true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));

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
            .Returns(Task.FromResult(Result.Success()));

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
            .Returns(Task.FromResult(Result.Error("Save failed.")));

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

    private IDayPlanningService RegisterServiceWithActivity(bool isCompleted)
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

        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [morning]
            })));
        Services.AddSingleton(service);
        return service;
    }
}
