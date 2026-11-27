using System.Net;
using AngleSharp.Html.Dom;
using NSubstitute;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityDeletionShould : BunitContext
{
    public PlannedActivityDeletionShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
    }

    [Fact]
    public void CancelDeletionWithoutChangingPlannedActivity()
    {
        // Arrange
        var service = RegisterServiceWithActivities(
            [new PlannedActivityDto { Id = 101, Name = "Morning walk" }]);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
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
        cut.Find("[data-testid='activity-detail-name']")
            .TextContent.Trim().ShouldBe("Morning walk");
        cut.Find("[data-testid='planned-activity-name']")
            .TextContent.ShouldBe("Morning walk");
        _ = service.DidNotReceive().DeletePlannedActivityAsync(
            Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeleteOnlySelectedActivityAfterConfirmation()
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
        var service = RegisterServiceWithActivities([selected, other]);
        service.DeletePlannedActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']")
            .Click();
        cut.Find("[data-testid='activity-detail-delete-button']")
            .Click();
        cut.Find("[data-testid='confirm-delete-button']")
            .Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).DeletePlannedActivityAsync(
                101, Arg.Any<CancellationToken>());

            cut.FindAll("[data-testid='planned-activity-name']")
                .Select(element => element.TextContent.Trim())
                .ToArray()
                .ShouldBe(["Read a book"]);
        });
    }

    [Fact]
    public async Task KeepOtherActivityOpenWhenPendingDeletionFinishes()
    {
        // Arrange
        var first = new PlannedActivityDto { Id = 101, Name = "Morning walk" };
        var second = new PlannedActivityDto { Id = 102, Name = "Read a book" };
        var pendingDeletion = new TaskCompletionSource<ClientResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = RegisterServiceWithActivities([first, second]);
        service.GetActivityAsync(102, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                HttpStatusCode.OK,
                new PlannedActivityDetailsDto
                {
                    Id = 102,
                    Name = "Read a book",
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlockId = 1,
                    TimeBlockName = "Morning",
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(9, 0)
                })));
        service.DeletePlannedActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(pendingDeletion.Task);
        var cut = Render<DayPlanning>();

        // Act
        cut.FindAll("[data-testid='activity-open-button']")[0].Click();
        cut.Find("[data-testid='activity-detail-delete-button']").Click();
        var deleteTask = cut.Find("[data-testid='confirm-delete-button']").ClickAsync();
        cut.Find("[data-testid='cancel-delete-button']").TextContent
            .ShouldBe("Sluiten");
        cut.Find("[data-testid='confirm-delete-button']")
            .GetAttribute("disabled").ShouldNotBeNull();
        cut.Find("[data-testid='cancel-delete-button']").Click();
        cut.Find(".activity-details-close").Click();
        cut.FindAll("[data-testid='activity-open-button']")[1].Click();
        pendingDeletion.SetResult(new ClientResult(HttpStatusCode.OK));
        await deleteTask;

        // Assert
        cut.Find("[data-testid='activity-detail-name']").TextContent
            .ShouldBe("Read a book");
        cut.FindAll("[data-testid='planned-activity-name']")
            .Select(element => element.TextContent.Trim())
            .ToArray()
            .ShouldBe(["Read a book"]);
    }

    [Fact]
    public async Task KeepOtherEditOpenWhenPendingDeletionFromFormFinishes()
    {
        // Arrange
        var first = new PlannedActivityDto { Id = 101, Name = "Morning walk" };
        var second = new PlannedActivityDto { Id = 102, Name = "Read a book" };
        var pendingDeletion = new TaskCompletionSource<ClientResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = RegisterServiceWithActivities([first, second]);
        service.GetActivityAsync(102, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                HttpStatusCode.OK,
                new PlannedActivityDetailsDto
                {
                    Id = 102,
                    Name = "Read a book",
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlockId = 1,
                    TimeBlockName = "Morning",
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(9, 0)
                })));
        service.DeletePlannedActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(pendingDeletion.Task);
        var cut = Render<DayPlanning>();

        // Act
        cut.FindAll("[data-testid='activity-open-button']")[0].Click();
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
        cut.Find("[data-testid='delete-activity-button']").Click();
        var deleteTask = cut.Find("[data-testid='confirm-delete-button']").ClickAsync();
        cut.Find("[data-testid='cancel-delete-button']").Click();
        cut.Find(".activity-form header button").Click();
        cut.Find(".activity-details-close").Click();
        cut.FindAll("[data-testid='activity-open-button']")[1].Click();
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
        cut.Find("[data-testid='activity-name']").Input("Updated book");
        pendingDeletion.SetResult(new ClientResult(HttpStatusCode.OK));
        await deleteTask;

        // Assert
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Updated book");
        cut.FindAll("[data-testid='planned-activity-name']")
            .Select(element => element.TextContent.Trim())
            .ToArray()
            .ShouldBe(["Read a book"]);
    }

    [Fact]
    public async Task ShowDeleteFailureInPlanningAfterClosingConfirmation()
    {
        // Arrange
        var pendingDeletion = new TaskCompletionSource<ClientResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = RegisterServiceWithActivities(
            [new PlannedActivityDto { Id = 101, Name = "Morning walk" }]);
        service.DeletePlannedActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(pendingDeletion.Task);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-detail-delete-button']").Click();
        var deleteTask = cut.Find("[data-testid='confirm-delete-button']").ClickAsync();
        cut.Find("[data-testid='cancel-delete-button']").Click();
        cut.Find(".activity-details-close").Click();
        pendingDeletion.SetResult(new ClientResult(HttpStatusCode.InternalServerError));
        await deleteTask;

        // Assert
        cut.Find("[role='alert']").TextContent
            .ShouldContain("Verwijderen is niet gelukt.");
        cut.Find("[data-testid='planned-activity-name']").TextContent
            .ShouldBe("Morning walk");
    }

    [Fact]
    public void KeepActivityVisibleAndShowErrorWhenDeletionFails()
    {
        // Arrange
        var service = RegisterServiceWithActivities(
            [new PlannedActivityDto { Id = 101, Name = "Morning walk" }]);
        service.DeletePlannedActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(
                HttpStatusCode.UnprocessableEntity, "Delete failed.")));
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-detail-delete-button']").Click();
        cut.Find("[data-testid='confirm-delete-button']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='activity-delete-error']")
                .TextContent.ShouldContain("Verwijderen is niet gelukt.");
            cut.Find("[data-testid='planned-activity-name']")
                .TextContent.ShouldBe("Morning walk");
            _ = service.Received(1).DeletePlannedActivityAsync(
                101, Arg.Any<CancellationToken>());
        });
    }

    private IDayPlanningClient RegisterServiceWithActivities(
        IReadOnlyList<PlannedActivityDto> activities)
    {
        var service = Substitute.For<IDayPlanningClient>();
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
        var morning = Block(1, "Morning", 6, 9);
        morning.Activities = activities;
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlocks = [morning]
                })));
        Services.AddSingleton(service);
        return service;
    }
}
