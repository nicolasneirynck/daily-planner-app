using System.Net;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using AngleSharp.Html.Dom;
using NSubstitute;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityCreationFailureShould : BunitContext
{
    public PlannedActivityCreationFailureShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
    }

    [Fact]
    public void ShowSaveErrorAndKeepFormOpenWhenSavingFails()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [Block(1, "Morning", 6, 9)]
            })));
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.UnprocessableEntity, "Save failed.")));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var name = (IHtmlInputElement)cut.Find(
                "[role='dialog'] [data-testid='activity-name']");
            name.Value.ShouldBe("Read a book");

            cut.Find("[data-testid='activity-save-error']")
                .TextContent.ShouldContain("Opslaan is niet gelukt.");
        });
    }

    [Fact]
    public void ShowSaveErrorWhenCreateRequestThrows()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [Block(1, "Morning", 6, 9)]
            })));
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ClientResult>(
                new HttpRequestException("Connection failed.")));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-save-error']")
                .TextContent.ShouldContain("Opslaan is niet gelukt."));
    }
}
