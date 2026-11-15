using Ardalis.Result;
using AngleSharp.Html.Dom;
using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
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

    [Fact]
    public async Task KeepSelectedDayWhenAnOlderRequestFinishesLast()
    {
        /*  1. De pagina toont vandaag
            2. Je klikt naar morgen; dat antwoord wordt vertraagd
            3. Je klikt terug naar vandaag; het antwoord voor vandaag komt meteen
            4. Daarna komt het vertraagde antwoord voor morgen alsnog binnen*/
        
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);
        var delayedTomorrow = new TaskCompletionSource<Result<DayPlanningResponse>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var date = call.ArgAt<DateOnly>(0);
                // Vraagt de pagina morgen, dan krijgt ze delayedTomorrow.Task. Die is nog niet klaar, dus de pagina blijft wachten.
                return date == tomorrow
                    ? delayedTomorrow.Task
                    : Task.FromResult(Day(date));
            });
        Services.AddSingleton(service);

        var cut = Render<DayPlanning>();
        await cut.WaitForAssertionAsync(() =>
            cut.Find("[data-testid='time-block-name']").TextContent.Trim()
                .ShouldBe($"Day {today:yyyy-MM-dd}"));

        // Act: start loading tomorrow, then return to today
        var nextClick = cut.Find("[data-testid='next-day-button']")
            .ClickAsync(new MouseEventArgs());

        await cut.WaitForAssertionAsync(() =>
        {
            _ = service.Received(1).GetDayAsync(tomorrow, Arg.Any<CancellationToken>());
        });

        await cut.Find("[data-testid='previous-day-button']")
            .ClickAsync(new MouseEventArgs()); // ClickAsync geeft een Task terug -> controle 

        // rond uitgestelde aanvraag voor morgen af
        delayedTomorrow.SetResult(Day(tomorrow)); // Day(tomorrow) maakt testantwoord via helper, SetResult geeft dat antwoord aan de awachtende GetDayAsync
        await nextClick; // wachten tot test de eerder gestarte klikafhandeling klaar is

        // Assert dat vandaag zichtbaar blijft, ook nadat een ouder antwoord voor morgen alsnog binnenkomt
        await cut.WaitForAssertionAsync(() =>
            cut.Find("[data-testid='time-block-name']").TextContent.Trim()
                .ShouldBe($"Day {today:yyyy-MM-dd}"));

        // kleine helper binnen test: maakt snel een geslaagd serviceantwoord voor een gekozen datum
        static Result<DayPlanningResponse> Day(DateOnly date) =>
            Result.Success(new DayPlanningResponse
            {
                Date = date,
                TimeBlocks = [Block(1, $"Day {date:yyyy-MM-dd}", 6, 9)]
            });
    }

    [Fact]
    public void PrefillDateAndTimeBlockWhenAddingFromTimeBlock()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = today,
                TimeBlocks = [Block(1, "Ochtend", 6, 9)]
            })));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();

        // Assert
        var dialog = cut.Find("[role='dialog']"); // role="dialog" hoort bij een venster dat boven de pagina opent
        dialog.TextContent.ShouldContain("Activiteit toevoegen");
        var date = (IHtmlInputElement)cut.Find(
            "[role='dialog'] [data-testid='activity-date']");
        date.Value.ShouldBe(today.ToString("yyyy-MM-dd"));

        var timeBlock = (IHtmlSelectElement)cut.Find(
            "[role='dialog'] [data-testid='activity-time-block']");
        timeBlock.Value.ShouldBe("1");
    }

    [Fact]
    public void PrefillOnlySelectedDateWhenAddingFromGlobalButton()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = call.ArgAt<DateOnly>(0),
                TimeBlocks = [Block(1, "Morning", 6, 9)]
            })));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(1);

        // Act
        cut.Find("[data-testid='next-day-button']").Click();
        cut.Find("[data-testid='global-add-activity-button']").Click();

        // Assert
        var dialog = cut.Find("[role='dialog']");
        dialog.TextContent.ShouldContain("Activiteit toevoegen");
        var date = (IHtmlInputElement)cut.Find(
            "[role='dialog'] [data-testid='activity-date']");
        date.Value.ShouldBe(tomorrow.ToString("yyyy-MM-dd"));

        var timeBlock = (IHtmlSelectElement)cut.Find(
            "[role='dialog'] [data-testid='activity-time-block']");
        timeBlock.Value.ShouldBe("");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectActivityNamesWithoutVisibleCharacters(string name)
    {
        // Arrange
        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [Block(1, "Morning", 6, 9)]
            })));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change(name);
        cut.Find("[data-testid='submit-activity-button']").Click();

        // Assert
        cut.Find("[role='alert']").TextContent.ShouldContain("Vul een naam in.");
        _ = service.DidNotReceive().CreateActivityAsync(
            Arg.Any<CreatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void PassEditedTimeBlockAndActivityDetailsToService()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = today,
                TimeBlocks =
                [
                    Block(1, "Morning", 6, 9),
                    Block(2, "Late morning", 9, 12)
                ]
            })));
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='activity-time-block']").Change("2");
        cut.Find("[data-testid='submit-activity-button']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).CreateActivityAsync(
                Arg.Is<CreatePlannedActivityRequest>(request =>
                    request.Date == today &&
                    request.TimeBlockId == 2 &&
                    request.Name == "Read a book"),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void SendChosenTimeBlockWhenAddingFromGlobalButton()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks =
                [
                    Block(1, "Morning", 6, 9),
                    Block(2, "Late morning", 9, 12)
                ]
            })));
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='global-add-activity-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='activity-time-block']").Change("2");
        cut.Find("[data-testid='submit-activity-button']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).CreateActivityAsync(
                Arg.Is<CreatePlannedActivityRequest>(request =>
                    request.TimeBlockId == 2),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void SendEditedDateWhenAddingFromTimeBlock()
    {
        // Arrange
        var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [Block(1, "Morning", 6, 9)]
            })));
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='activity-date']")
            .Change(tomorrow.ToString("yyyy-MM-dd"));
        cut.Find("[data-testid='submit-activity-button']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).CreateActivityAsync(
                Arg.Is<CreatePlannedActivityRequest>(request =>
                    request.Date == tomorrow),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void RequireTimeBlockWhenAddingFromGlobalButton()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningService>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [Block(1, "Morning", 6, 9)]
            })));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='global-add-activity-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='submit-activity-button']").Click();

        // Assert
        cut.Find("[data-testid='activity-time-block-error']")
            .TextContent.ShouldContain("Kies een tijdsblok.");
        _ = service.DidNotReceive().CreateActivityAsync(
            Arg.Any<CreatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());
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
