using Ardalis.Result;
using NSubstitute;
using Planning.Shared.DayPlanning;
using Shouldly;

namespace Planning.Client.Pages;

public class DayPlanningShould : BunitContext
{
    [Fact]
    public void ShowAllEmptyTimeBlocksInSortOrder()
    {
        // Arrange
        var blocks = new[]
        {
            Block(3, "Middag", 12, 14),
            Block(1, "Ochtend", 6, 9),
            Block(6, "Avond", 18, 23),
            Block(2, "Voormiddag", 9, 12),
            Block(5, "Late namiddag", 16, 18),
            Block(4, "Vroege namiddag", 14, 16)
        };

        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = blocks
            })));
        Services.AddSingleton(service);

        // Act
        var cut = Render<DayPlanning>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='time-block-name']")
                .Select(heading => heading.TextContent.Trim())
                .ToArray()
                .ShouldBe([
                    "Ochtend",
                    "Voormiddag",
                    "Middag",
                    "Vroege namiddag",
                    "Late namiddag",
                    "Avond"
                ]);
        });
    }

    [Fact]
    public void ShowActivitiesInsideTheirTimeBlocks()
    {
        // Arrange
        var morning = Block(1, "Ochtend", 6, 9);
        morning.Activities =
        [
            new PlannedActivityDto { Id = 101, Name = "Morning walk" },
            new PlannedActivityDto { Id = 102, Name = "Read a book" }
        ];

        var midday = Block(3, "Middag", 12, 14);

        var evening = Block(6, "Avond", 18, 23);
        evening.Activities =
        [
            new PlannedActivityDto { Id = 103, Name = "Evening routine" }
        ];

        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [morning, midday, evening]
            })));
        Services.AddSingleton(service);

        // Act
        var cut = Render<DayPlanning>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            // De test zoekt dus alle elementen met data-testid="planned-activity-name" binnen het tijdsblok met data-testid="time-block-1". Dat mogen directe kinderen zijn, maar ook dieper geneste elementen.
            cut.FindAll("[data-testid='time-block-1'] [data-testid='planned-activity-name']")
                .Select(activity => activity.TextContent.Trim())
                .ToArray()
                .ShouldBe(["Morning walk", "Read a book"]);

            cut.FindAll("[data-testid='time-block-3'] [data-testid='planned-activity-name']")
                .ShouldBeEmpty();

            cut.FindAll("[data-testid='time-block-6'] [data-testid='planned-activity-name']")
                .Select(activity => activity.TextContent.Trim())
                .ToArray()
                .ShouldBe(["Evening routine"]);
        });
    }

    [Fact]
    public void RequestCurrentDayWhenOpened()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today)
            })));
        Services.AddSingleton(service);
        var dateBeforeRender = DateOnly.FromDateTime(DateTime.Today);

        // Act
        var cut = Render<DayPlanning>();
        var dateAfterRender = DateOnly.FromDateTime(DateTime.Today);

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).GetDayAsync(
                Arg.Is<DateOnly>(date => date >= dateBeforeRender && date <= dateAfterRender),
                Arg.Any<CancellationToken>());
        });
    }

    private static TimeBlockDto Block(int order, string name, int startHour, int endHour) =>
        new()
        {
            Id = order,
            SortOrder = order,
            Name = name,
            StartTime = new TimeOnly(startHour, 0),
            EndTime = new TimeOnly(endHour, 0)
        };
}
