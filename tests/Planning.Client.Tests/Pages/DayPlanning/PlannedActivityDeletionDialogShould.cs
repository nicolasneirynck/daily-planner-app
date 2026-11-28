using System.Net;
using NSubstitute;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityDeletionDialogShould : BunitContext
{
    public PlannedActivityDeletionDialogShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
    }

    [Fact]
    public void CancelDeletionFromDetailsWithoutChangingPlannedActivity()
    {
        // Arrange
        var service = RegisterServiceWithActivities(() =>
        [
            new PlannedActivityDto { Id = 101, Name = "Morning walk" }
        ]);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.WaitForAssertion(() =>
            cut.Find("dialog[aria-label='Activiteit bekijken'] [data-testid='activity-detail-delete-button']")
                .ShouldNotBeNull());
        cut.Find("[data-testid='activity-detail-delete-button']").Click();

        // Assert: confirmation is required
        var confirmation = cut.Find("[role='alertdialog']");
        confirmation.TextContent.ShouldContain("Verwijderen uit planning?");
        confirmation.TextContent.ShouldContain(
            "“Morning walk” wordt verwijderd uit je dagplanning.");
        /* FindAll("[role='alertdialog']") zoekt alle bevestigingsvensters in de gerenderde component. ShouldBeEmpty() controleert dat er geen gevonden zijn. */
        _ = service.DidNotReceive().DeletePlannedActivityAsync(
            Arg.Any<int>(), Arg.Any<CancellationToken>());

        // Act
        cut.Find("[data-testid='cancel-delete-button']").Click();

        // Assert: the activity and its details remain
        cut.FindAll("[role='alertdialog']").ShouldBeEmpty();
        cut.Find("dialog[aria-label='Activiteit bekijken'] [data-testid='activity-detail-name']")
            .TextContent.ShouldBe("Morning walk");
        cut.Find("[data-testid='planned-activity-name']")
            .TextContent.ShouldBe("Morning walk");
        _ = service.DidNotReceive().DeletePlannedActivityAsync(
            Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeleteOnlySelectedActivityFromDetailsAfterConfirmation()
    {
        // Arrange
        var selected = new PlannedActivityDto
        {
            Id = 101,
            Name = "Morning walk"
        };
        var other = new PlannedActivityDto
        {
            Id = 102,
            Name = "Read a book"
        };
        IReadOnlyList<PlannedActivityDto> activities = [selected, other];

        var service = RegisterServiceWithActivities(() => activities);
        service.DeletePlannedActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                activities = [other];
                return Task.FromResult(new ClientResult(HttpStatusCode.OK));
            });
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']")
            .Click();
        cut.WaitForAssertion(() =>
            cut.Find("dialog[aria-label='Activiteit bekijken'] [data-testid='activity-detail-delete-button']")
                .ShouldNotBeNull());
        cut.Find("[data-testid='activity-detail-delete-button']")
            .Click();
        cut.Find("[data-testid='confirm-delete-button']")
            .Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).DeletePlannedActivityAsync(
                101, Arg.Any<CancellationToken>());

            cut.FindAll("dialog[aria-label='Activiteit bekijken']").ShouldBeEmpty();
            cut.FindAll("[role='alertdialog']").ShouldBeEmpty();
            cut.FindAll("[data-testid='planned-activity-name']")
                .Select(element => element.TextContent.Trim())
                .ToArray()
                .ShouldBe(["Read a book"]);
        });
    }

    [Fact]
    public void KeepActivityInDetailsAndShowErrorWhenDeletionFails()
    {
        // Arrange
        var service = RegisterServiceWithActivities(() =>
        [
            new PlannedActivityDto { Id = 101, Name = "Morning walk" }
        ]);
        service.DeletePlannedActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(
                HttpStatusCode.UnprocessableEntity, "Delete failed.")));
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.WaitForAssertion(() =>
            cut.Find("dialog[aria-label='Activiteit bekijken'] [data-testid='activity-detail-delete-button']")
                .ShouldNotBeNull());
        cut.Find("[data-testid='activity-detail-delete-button']").Click();
        cut.Find("[data-testid='confirm-delete-button']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='activity-delete-error']")
                .TextContent.ShouldContain("Verwijderen is niet gelukt.");
            cut.Find("[data-testid='planned-activity-name']")
                .TextContent.ShouldBe("Morning walk");
            cut.Find("dialog[aria-label='Activiteit bekijken'] [data-testid='activity-detail-name']")
                .TextContent.ShouldBe("Morning walk");
            _ = service.Received(1).DeletePlannedActivityAsync(
                101, Arg.Any<CancellationToken>());
        });
    }

    /*  getActivities wordt bij elke GetDayAsync-aanroep opnieuw uitgevoerd.
        Zo kan de nepservice eerst de oorspronkelijke activiteiten teruggeven
        en na een verwijdering een bijgewerkte lijst.
        Dat bootst het opnieuw laden van de dagplanning na.

       Als je één vaste lijst meegeeft, geeft de nepservice bij het herladen steeds diezelfde lijst terug.
       Een veranderbare lijst zou ook kunnen;
       de functie maakt duidelijk dat elke aanroep de huidige toestand moet ophalen.*/
    private IDayPlanningClient RegisterServiceWithActivities(
        Func<IReadOnlyList<PlannedActivityDto>> getActivities) // Func<IReadOnlyList<PlannedActivityDto>> getActivities is een functie die de huidige activiteitenlijst teruggeeft.
    {
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var morning = Block(1, "Morning", 6, 9);
                morning.Activities = getActivities();

                return Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
                {
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlocks = [morning]
                }));
            });
        service.GetActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var activity = getActivities().Single(item => item.Id == 101);
                return Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                    HttpStatusCode.OK,
                    new PlannedActivityDetailsDto
                    {
                        Id = activity.Id,
                        Name = activity.Name,
                        Date = DateOnly.FromDateTime(DateTime.Today),
                        TimeBlockId = 1,
                        TimeBlockName = "Morning",
                        StartTime = new TimeOnly(6, 0),
                        EndTime = new TimeOnly(9, 0)
                    }));
            });
        Services.AddSingleton(service);
        return service;
    }
}
