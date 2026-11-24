using System.Net;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class DayPlanningNavigationShould : BunitContext
{
    [Fact]
    public void RequestCurrentDayWhenOpened()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
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
        var service = Substitute.For<IDayPlanningClient>();

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

                return Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
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
                .ShouldBe(ExpectedDateLabel(expectedDate));

            cut.Find("[data-testid='planned-activity-name']").TextContent.Trim()
                .ShouldBe($"Task on {expectedDate:yyyy-MM-dd}");

            Services.GetRequiredService<NavigationManager>().Uri
                .ShouldEndWith($"/dayplanning?date={expectedDate:yyyy-MM-dd}");
        });
    }

    [Fact]
    public void NavigateToPreviousDay()
    {
        // Arrange
        var requestedDates = new List<DateOnly>();
        var service = Substitute.For<IDayPlanningClient>();

        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var date = call.ArgAt<DateOnly>(0);
                requestedDates.Add(date);

                return Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
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
                .ShouldBe(ExpectedDateLabel(expectedDate));
        });
    }

    [Fact]
    public void ReturnToTodayFromAnotherDay()
    {
        // Arrange
        var requestedDates = new List<DateOnly>();
        var service = Substitute.For<IDayPlanningClient>();

        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var date = call.ArgAt<DateOnly>(0);
                requestedDates.Add(date);

                return Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
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
        var delayedTomorrow = new TaskCompletionSource<ClientResult<DayPlanningResponse>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var service = Substitute.For<IDayPlanningClient>();
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
        static ClientResult<DayPlanningResponse> Day(DateOnly date) =>
            new(HttpStatusCode.OK, new DayPlanningResponse
            {
                Date = date,
                TimeBlocks = [Block(1, $"Day {date:yyyy-MM-dd}", 6, 9)]
            });
    }

    private static string ExpectedDateLabel(DateOnly date)
    {
        var culture = CultureInfo.GetCultureInfo("nl-BE");
        var text = date.ToString("dddd d MMMM", culture);
        return $"{char.ToUpper(text[0], culture)}{text[1..]}";
    }
}
