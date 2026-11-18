using Ardalis.Result;
using AngleSharp.Html.Dom;
using NSubstitute;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class DayPlanningActivityCreationShould : BunitContext
{
    public DayPlanningActivityCreationShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
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
        string.IsNullOrEmpty(timeBlock.Value).ShouldBeTrue();
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
    public void ClearNameErrorWhenVisibleTextIsTyped()
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
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='submit-activity-button']").Click();

        // Act and assert
        cut.Find("[data-testid='activity-name']").Input("   ");
        cut.Find("[role='alert']").TextContent.ShouldContain("Vul een naam in.");

        cut.Find("[data-testid='activity-name']").Input("   Walk");
        cut.FindAll("[role='alert']").ShouldBeEmpty();
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

    [Fact]
    public void ShowSaveErrorAndKeepFormOpenWhenSavingFails()
    {
        // Arrange
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
            .Returns(Task.FromResult(Result.Error("Save failed.")));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='submit-activity-button']").Click();

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
            .Returns(_ => Task.FromException<Result>(
                new HttpRequestException("Connection failed.")));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='submit-activity-button']").Click();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='activity-save-error']")
                .TextContent.ShouldContain("Opslaan is niet gelukt."));
    }
}
