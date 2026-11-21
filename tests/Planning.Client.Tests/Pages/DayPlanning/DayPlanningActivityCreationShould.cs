using System.Net;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
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
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
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
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
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
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
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
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.Find("[role='alert']").TextContent.ShouldContain("Vul een naam in.");
        _ = service.DidNotReceive().CreateActivityAsync(
            Arg.Any<CreatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RejectActivityNameLongerThan250CharactersWithoutCallingApi()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [Block(1, "Morning", 6, 9)]
            })));

        Services.AddSingleton(service);

        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change(new string('a', 251));
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.Find("#activity-name-error")
            .TextContent.ShouldContain("maximaal 250 tekens");
        cut.Find("[data-testid='activity-name']")
            .GetAttribute("aria-invalid").ShouldBe("true");
        cut.Find("[data-testid='activity-name']")
            .GetAttribute("aria-describedby").ShouldBe("activity-name-error");
        _ = service.DidNotReceive().CreateActivityAsync(
            Arg.Any<CreatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ClearNameErrorWhenVisibleTextIsTyped()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [Block(1, "Morning", 6, 9)]
            })));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[role='dialog'] form").Submit();

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
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.OK)));
        Services.AddSingleton(service);
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

    [Fact]
    public void RequireTimeBlockWhenAddingFromGlobalButton()
    {
        // Arrange
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(HttpStatusCode.OK, new DayPlanningResponse
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                TimeBlocks = [Block(1, "Morning", 6, 9)]
            })));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='global-add-activity-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[role='dialog'] form").Submit();

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
    public void ShowServerNameValidationError()
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
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.BadRequest, "The request is invalid.")
            {
                FieldErrors = [new ClientFieldError("name", "Activity name is required.")]
            }));
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
            cut.Find("#activity-name-error").TextContent
                .ShouldBe("Activity name is required.");
            cut.Find("[data-testid='activity-name']")
                .GetAttribute("aria-invalid").ShouldBe("true");
            cut.Find("[data-testid='activity-name']")
                .GetAttribute("aria-describedby").ShouldBe("activity-name-error");
            _ = service.Received(1).CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void ClearServerNameValidationErrorWhenNameChanges()
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
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.BadRequest, "The request is invalid.")
            {
                FieldErrors = [new ClientFieldError("name", "Activity name is required.")]
            }));

        Services.AddSingleton(service);

        var cut = Render<DayPlanning>();

        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[role='dialog'] form").Submit();
        cut.WaitForAssertion(() =>
            cut.Find("#activity-name-error").TextContent
                .ShouldBe("Activity name is required."));

        // Act
        cut.Find("[data-testid='activity-name']").Input("Read another book");

        // Assert
        cut.FindAll("#activity-name-error").ShouldBeEmpty();
        var nameInput = cut.Find("[data-testid='activity-name']");
        nameInput.GetAttribute("aria-invalid").ShouldBe("false");
        nameInput.GetAttribute("aria-describedby").ShouldBeNull();
    }

    [Fact]
    public void AllowRetryAfterServerNameValidationError()
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
            .Returns(Task.FromResult(new ClientResult(HttpStatusCode.BadRequest, "The request is invalid.")
            {
                FieldErrors = [new ClientFieldError("name", "Activity name is required.")]
            }));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[role='dialog'] form").Submit();
        cut.WaitForAssertion(() =>
            cut.Find("#activity-name-error").TextContent
                .ShouldBe("Activity name is required."));

        // Act
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(2).CreateActivityAsync(
                Arg.Is<CreatePlannedActivityRequest>(request =>
                    request.Name == "Read a book"),
                Arg.Any<CancellationToken>());
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
