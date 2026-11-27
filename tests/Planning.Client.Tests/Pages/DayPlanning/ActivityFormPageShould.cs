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
    public ActivityFormPageShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
    }

    [Fact]
    public void CreateActivityFromPageWithSelectedTimeBlockAndReturnToDay()
    {
        // Arrange
        var selectedDate = new DateOnly(2026, 11, 24);
        var service = RegisterSuccessfulDayPlanningClient(selectedDate);
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(
            "/dayplanning/activities/new?date=2026-11-24&timeBlockId=1");
        var cut = Render<ActivityFormPage>();

        cut.WaitForAssertion(() =>
        {
            ((IHtmlInputElement)cut.Find("[data-testid='activity-date']"))
                .Value.ShouldBe("2026-11-24");
            ((IHtmlSelectElement)cut.Find("[data-testid='activity-time-block']"))
                .Value.ShouldBe("1");
        });

        // Act
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).CreateActivityAsync(
                Arg.Is<CreatePlannedActivityRequest>(request =>
                    request.Date == selectedDate &&
                    request.TimeBlockId == 1 &&
                    request.Name == "Read a book"),
                Arg.Any<CancellationToken>());
            navigation.Uri
                .ShouldEndWith("/dayplanning?date=2026-11-24");
        });
    }

    [Fact]
    public void SaveEditedActivityFromPageAndReturnToUpdatedDetails()
    {
        // Arrange
        var originalDate = new DateOnly(2026, 11, 24);
        var updatedDate = originalDate.AddDays(1);

        var service = RegisterActivityForEditing(originalDate);

        service.UpdateActivityAsync(
                101,
                Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.NoContent)));

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/dayplanning/activities/101/edit?date=2026-11-23");
        var cut = Render<ActivityFormPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));

        cut.WaitForAssertion(() =>
        {
            ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
                .Value.ShouldBe("Morning walk");
            ((IHtmlInputElement)cut.Find("[data-testid='activity-date']"))
                .Value.ShouldBe("2026-11-24");
            _ = service.Received(1).GetDayAsync(
                originalDate, Arg.Any<CancellationToken>());
        });

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
                .ShouldEndWith("/dayplanning/activities/101?date=2026-11-25");
        });
    }

    [Fact]
    public async Task KeepAnotherEditPageOpenWhenEarlierSaveFinishes()
    {
        // Arrange
        var date = new DateOnly(2026, 11, 24);
        var pendingSave = new TaskCompletionSource<ClientResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Substitute.For<IDayPlanningClient>();
        service.GetActivityAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.ArgAt<int>(0);
                var details = MorningWalkDetails(date);
                details.Id = id;
                details.Name = id == 101 ? "Morning walk" : "Read a book";
                return Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                    HttpStatusCode.OK, details));
            });
        service.GetDayAsync(date, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = date,
                    TimeBlocks = [Block(1, "Ochtend", 6, 9)]
                })));
        service.UpdateActivityAsync(101, Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(pendingSave.Task);
        Services.AddSingleton(service);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/dayplanning/activities/101/edit?date=2026-11-24");
        var cut = Render<ActivityFormPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));
        cut.WaitForAssertion(() =>
            ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
                .Value.ShouldBe("Morning walk"));

        // Act
        cut.Find("[data-testid='activity-name']").Change("Evening walk");
        var saveTask = cut.Find("form").SubmitAsync();
        cut.Render(parameters => parameters
            .Add(page => page.ActivityId, 102));
        cut.WaitForAssertion(() =>
            ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
                .Value.ShouldBe("Read a book"));
        navigation.NavigateTo("/dayplanning/activities/102/edit?date=2026-11-24");
        cut.Find("[data-testid='activity-name']").Input("Updated book");
        pendingSave.SetResult(new ClientResult(HttpStatusCode.NoContent));
        await saveTask;

        // Assert
        navigation.Uri.ShouldEndWith("/dayplanning/activities/102/edit?date=2026-11-24");
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Updated book");
    }

    [Fact]
    public void KeepPageEditsUntilUserDiscardsThem()
    {
        // Arrange
        var originalDate = new DateOnly(2026, 11, 24);
        var service = RegisterActivityForEditing(originalDate);

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/dayplanning/activities/101/edit?date=2026-11-24");
        var cut = Render<ActivityFormPage>(parameters => parameters
            .Add(page => page.ActivityId, 101));

        cut.WaitForAssertion(() =>
            ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
                .Value.ShouldBe("Morning walk"));

        // Act
        cut.Find("[data-testid='activity-name']").Change("Evening walk");
        cut.Find("button[aria-label='Terug']").Click();

        // Assert
        cut.Find("[role='alertdialog']").TextContent
            .ShouldContain("Niet-opgeslagen wijzigingen");

        // Act
        cut.Find("[data-testid='continue-editing-button']").Click();

        // Assert
        cut.FindAll("[role='alertdialog']").ShouldBeEmpty();
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Evening walk");

        // Act
        cut.Find("button[aria-label='Terug']").Click();
        cut.Find("[data-testid='discard-changes-button']").Click();

        // Assert
        Services.GetRequiredService<NavigationManager>().Uri
            .ShouldEndWith("/dayplanning/activities/101?date=2026-11-24");
        _ = service.DidNotReceive().UpdateActivityAsync(
            Arg.Any<int>(),
            Arg.Any<UpdatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());
    }

    private IDayPlanningClient RegisterSuccessfulDayPlanningClient(DateOnly date)
    {
        var morning = Block(1, "Ochtend", 6, 9);
        var service = Substitute.For<IDayPlanningClient>();

        service.GetDayAsync(date, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = date,
                    TimeBlocks = [morning]
                })));

        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));

        Services.AddSingleton(service);
        return service;
    }

    private IDayPlanningClient RegisterActivityForEditing(DateOnly date)
    {
        var service = Substitute.For<IDayPlanningClient>();

        service.GetActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                HttpStatusCode.OK,
                MorningWalkDetails(date))));

        service.GetDayAsync(date, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = date,
                    TimeBlocks = [Block(1, "Ochtend", 6, 9)]
                })));

        Services.AddSingleton(service);
        return service;
    }

    private static PlannedActivityDetailsDto MorningWalkDetails(DateOnly date) => new()
    {
        Id = 101,
        Name = "Morning walk",
        Date = date,
        TimeBlockId = 1,
        TimeBlockName = "Ochtend",
        StartTime = new TimeOnly(6, 0),
        EndTime = new TimeOnly(9, 0)
    };
}
