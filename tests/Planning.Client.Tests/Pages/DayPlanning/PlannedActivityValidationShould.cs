using System.Net;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using NSubstitute;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityValidationShould : BunitContext
{
    public PlannedActivityValidationShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
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

}
