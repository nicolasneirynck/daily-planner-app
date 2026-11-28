using System.Net;
using NSubstitute;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityDetailsDialogShould : BunitContext
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ToggleActivityCompletionFromDesktopDetailsAndUpdateDayPlanning(
        bool initialStatus,
        bool updatedStatus)
    {
        // Arrange
        var service = RegisterServiceWithActivity(initialStatus);
        service.SetActivityCompletionAsync(101, updatedStatus, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.NoContent)));
        var cut = Render<DayPlanning>();

        // Act
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-open-button']").ShouldNotBeNull());
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.WaitForAssertion(() =>
            cut.Find("dialog[aria-label='Activiteit bekijken'] [data-testid='activity-detail-completion-toggle']")
                .ShouldNotBeNull());
        cut.Find("dialog[aria-label='Activiteit bekijken'] [data-testid='activity-detail-completion-toggle']")
            .Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).SetActivityCompletionAsync(
                101, updatedStatus, Arg.Any<CancellationToken>());
            var expectedPressed = updatedStatus ? "true" : "false";
            cut.Find("[data-testid='activity-detail-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe(expectedPressed);
            cut.Find("[data-testid='activity-completion-toggle']")
                .GetAttribute("aria-pressed").ShouldBe(expectedPressed);
        });
    }

    [Fact]
    public void KeepCompletionStatusAndShowErrorWhenDesktopDetailsUpdateFails()
    {
        // Arrange
        var service = RegisterServiceWithActivity(isCompleted: false);
        service.SetActivityCompletionAsync(101, true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(
                HttpStatusCode.UnprocessableEntity, "Save failed.")));
        var cut = Render<DayPlanning>();

        // Act
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-open-button']").ShouldNotBeNull());
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-detail-completion-toggle']").ShouldNotBeNull());
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
            cut.Find("dialog[aria-label='Activiteit bekijken'] [role='alert']")
                .TextContent.ShouldContain("Status wijzigen is niet gelukt.");
        });
    }

    private IDayPlanningClient RegisterServiceWithActivity(bool isCompleted)
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
        var today = DateOnly.FromDateTime(DateTime.Today);
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
        var details = new PlannedActivityDetailsDto
        {
            Id = 101,
            Name = "Morning walk",
            Date = today,
            TimeBlockId = 1,
            TimeBlockName = "Morning",
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(9, 0),
            IsCompleted = isCompleted
        };

        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse { Date = today, TimeBlocks = [morning] })));
        service.GetActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                HttpStatusCode.OK, details)));
        Services.AddSingleton(service);
        return service;
    }
}
