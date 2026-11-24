using System.Net;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using AngleSharp.Html.Dom;
using NSubstitute;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityCreationShould : BunitContext
{
    public PlannedActivityCreationShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
    }

    [Fact]
    public void PrefillDateAndTimeBlockWhenAddingFromTimeBlock()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        RegisterPlanningWithSingleTimeBlock("Ochtend");
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
        RegisterPlanningWithSingleTimeBlock();
        var cut = Render<DayPlanning>();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(1);

        // Act
        cut.Find("[data-testid='next-day-button']").Click();
        var mobileUrl = cut.Find("[data-testid='mobile-global-add-activity-link']")
            .GetAttribute("href");
        cut.Find("[data-testid='global-add-activity-button']").Click();

        // Assert
        mobileUrl.ShouldBe($"/dayplanning/activities/new?date={tomorrow:yyyy-MM-dd}");
        var dialog = cut.Find("[role='dialog']");
        dialog.TextContent.ShouldContain("Activiteit toevoegen");
        var date = (IHtmlInputElement)cut.Find(
            "[role='dialog'] [data-testid='activity-date']");
        date.Value.ShouldBe(tomorrow.ToString("yyyy-MM-dd"));

        var timeBlock = (IHtmlSelectElement)cut.Find(
            "[role='dialog'] [data-testid='activity-time-block']");
        string.IsNullOrEmpty(timeBlock.Value).ShouldBeTrue();
    }

    [Fact]
    public void WarnBeforeClosingNewActivityWithUnsavedChanges()
    {
        // Arrange
        var service = RegisterPlanningWithSingleTimeBlock();
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='global-add-activity-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[role='dialog'] button[aria-label='Sluiten']").Click();

        // Assert
        cut.Find("[role='alertdialog']").TextContent
            .ShouldContain("Niet-opgeslagen wijzigingen");
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Read a book");
        _ = service.DidNotReceive().CreateActivityAsync(
            Arg.Any<CreatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void KeepNewActivityDetailsWhenContinuingToEdit()
    {
        // Arrange
        RegisterPlanningWithSingleTimeBlock();
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='global-add-activity-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[role='dialog'] button[aria-label='Sluiten']").Click();
        cut.Find("[data-testid='continue-editing-button']").Click();

        // Assert
        cut.FindAll("[role='alertdialog']").ShouldBeEmpty();
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Read a book");
    }

    [Fact]
    public void DiscardNewActivityDetailsWhenClosingWithoutSaving()
    {
        // Arrange
        var service = RegisterPlanningWithSingleTimeBlock();
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='global-add-activity-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[role='dialog'] button[aria-label='Sluiten']").Click();
        cut.Find("[data-testid='discard-changes-button']").Click();

        // Assert
        cut.FindAll("[role='dialog']").ShouldBeEmpty();
        cut.FindAll("[role='alertdialog']").ShouldBeEmpty();
        _ = service.DidNotReceive().CreateActivityAsync(
            Arg.Any<CreatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());

        // Act: reopen the form
        cut.Find("[data-testid='global-add-activity-button']").Click();

        // Assert
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe(string.Empty);
    }

    [Fact]
    public void CloseUnchangedNewActivityWithoutWarning()
    {
        // Arrange
        RegisterPlanningWithSingleTimeBlock();
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='global-add-activity-button']").Click();
        cut.Find("[role='dialog'] button[aria-label='Sluiten']").Click();

        // Assert
        cut.FindAll("[role='dialog']").ShouldBeEmpty();
        cut.FindAll("[role='alertdialog']").ShouldBeEmpty();
    }

    [Fact]
    public void CloseNewActivityWithoutWarningAfterRevertingChanges()
    {
        // Arrange
        RegisterPlanningWithSingleTimeBlock();
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='global-add-activity-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='activity-name']").Change(string.Empty);
        cut.Find("[role='dialog'] button[aria-label='Sluiten']").Click();

        // Assert
        cut.FindAll("[role='dialog']").ShouldBeEmpty();
        cut.FindAll("[role='alertdialog']").ShouldBeEmpty();
    }

    [Fact]
    public void PassEditedTimeBlockAndActivityDetailsToService()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
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
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='activity-time-block']").Change("2");
        cut.Find("[role='dialog'] form").Submit();

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
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
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
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='global-add-activity-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='activity-time-block']").Change("2");
        cut.Find("[role='dialog'] form").Submit();

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
        var service = RegisterPlanningWithSingleTimeBlock();
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='activity-date']")
            .Change(tomorrow.ToString("yyyy-MM-dd"));
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).CreateActivityAsync(
                Arg.Is<CreatePlannedActivityRequest>(request =>
                    request.Date == tomorrow),
                Arg.Any<CancellationToken>());
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bring the library card")]
    public void PassOptionalDescriptionToService(string? description)
    {
        // Arrange
        var service = RegisterPlanningWithSingleTimeBlock();
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        if (description is not null)
            cut.Find("[data-testid='activity-description']").Input(description);
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).CreateActivityAsync(
                Arg.Is<CreatePlannedActivityRequest>(request =>
                    request.Description == description),
                Arg.Any<CancellationToken>());
        });
    }

    private IDayPlanningClient RegisterPlanningWithSingleTimeBlock(
        string blockName = "Morning")
    {
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = call.ArgAt<DateOnly>(0),
                    TimeBlocks = [Block(1, blockName, 6, 9)]
                })));
        Services.AddSingleton(service);
        return service;
    }
}
