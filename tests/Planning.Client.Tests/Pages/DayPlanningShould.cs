using Ardalis.Result;
using System.Globalization;
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

    [Fact]
    public void NavigateForwardFromSelectedDay()
    {
        // Arrange
        var requestedDates = new List<DateOnly>();
        var service = Substitute.For<IDayPlanningService>();

        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var date = call.ArgAt<DateOnly>(0);
                requestedDates.Add(date);

                var morning = Block(1, "Morning", 6, 9);
                morning.Activities =
                [
                    new PlannedActivityDto
                    {
                        Id = 1,
                        Name = $"Task on {date:yyyy-MM-dd}"
                    }
                ];

                return Task.FromResult(Result.Success(new DayPlanningResponse
                {
                    Date = date,
                    TimeBlocks = [morning]
                }));
            });
        Services.AddSingleton(service);

        var cut = Render<DayPlanning>();
        cut.WaitForAssertion(() => requestedDates.Count.ShouldBe(1));
        var startingDate = requestedDates.Single();

        // Act
        cut.Find("[data-testid='next-day-button']").Click();
        cut.Find("[data-testid='next-day-button']").Click();

        // Assert
        var expectedDate = startingDate.AddDays(2);
        cut.WaitForAssertion(() =>
        {
            requestedDates.ToArray()
                .ShouldBe([startingDate, startingDate.AddDays(1), expectedDate]);

            cut.Find("[data-testid='selected-date']").TextContent.Trim()
                .ShouldBe(expectedDate.ToString(
                    "dddd d MMMM", CultureInfo.GetCultureInfo("nl-BE")));

            cut.Find("[data-testid='planned-activity-name']").TextContent.Trim()
                .ShouldBe($"Task on {expectedDate:yyyy-MM-dd}");
        });
    }

    [Fact]
    public void NavigateToPreviousDay()
    {
        // Arrange
        var requestedDates = new List<DateOnly>();
        var service = Substitute.For<IDayPlanningService>();

        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var date = call.ArgAt<DateOnly>(0);
                requestedDates.Add(date);

                return Task.FromResult(Result.Success(new DayPlanningResponse
                {
                    Date = date
                }));
            });
        Services.AddSingleton(service);

        var cut = Render<DayPlanning>();
        cut.WaitForAssertion(() => requestedDates.Count.ShouldBe(1));
        var startingDate = requestedDates.Single();

        // Act
        cut.Find("[data-testid='previous-day-button']").Click();

        // Assert
        var expectedDate = startingDate.AddDays(-1);
        cut.WaitForAssertion(() =>
        {
            requestedDates.ToArray()
                .ShouldBe([startingDate, expectedDate]);

            cut.Find("[data-testid='selected-date']").TextContent.Trim()
                .ShouldBe(expectedDate.ToString(
                    "dddd d MMMM", CultureInfo.GetCultureInfo("nl-BE")));
        });
    }

    [Fact]
    public void ReturnToTodayFromAnotherDay()
    {
        // Arrange
        var requestedDates = new List<DateOnly>();
        var service = Substitute.For<IDayPlanningService>();

        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var date = call.ArgAt<DateOnly>(0);
                requestedDates.Add(date);

                return Task.FromResult(Result.Success(new DayPlanningResponse
                {
                    Date = date
                }));
            });
        Services.AddSingleton(service);

        var cut = Render<DayPlanning>();
        cut.WaitForAssertion(() => requestedDates.Count.ShouldBe(1));
        var today = requestedDates.Single();

        // Assert: today starts active
        cut.Find("[data-testid='today-button']")
            .GetAttribute("aria-pressed").ShouldBe("true");

        // Act: leave today
        cut.Find("[data-testid='next-day-button']").Click();

        // Assert: today is no longer active
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='today-button']")
                .GetAttribute("aria-pressed").ShouldBe("false"));

        // Act: return to today
        cut.Find("[data-testid='today-button']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            requestedDates.ToArray()
                .ShouldBe([today, today.AddDays(1), today]);

            cut.Find("[data-testid='today-button']")
                .GetAttribute("aria-pressed").ShouldBe("true");
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
