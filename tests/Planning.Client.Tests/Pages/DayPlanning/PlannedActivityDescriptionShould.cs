using System.Net;
using NSubstitute;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityDescriptionShould : BunitContext
{
    public PlannedActivityDescriptionShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
    }

    [Fact]
    public void ShowDescriptionCharacterCountWhileTyping()
    {
        // Arrange
        RegisterMorningBlock();
        var cut = Render<DayPlanning>();
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();

        // Assert
        cut.Find("[data-testid='activity-description-count']")
            .TextContent.Trim().ShouldBe("0/500");

        // Act
        cut.Find("[data-testid='activity-description']")
            .Input("Bring the library card");

        // Assert
        cut.Find("[data-testid='activity-description-count']")
            .TextContent.Trim().ShouldBe("22/500");
    }

    [Fact]
    public void ShowDescriptionLimitAt500Characters()
    {
        // Arrange
        RegisterMorningBlock();
        var cut = Render<DayPlanning>();
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();

        // Act
        cut.Find("[data-testid='activity-description']")
            .Input(new string('a', 500));

        // Assert
        cut.Find("[data-testid='activity-description']")
            .GetAttribute("maxlength").ShouldBe("500");
        cut.Find("[data-testid='activity-description-count']")
            .TextContent.Trim().ShouldBe("500/500");
        cut.Find("[data-testid='activity-description-limit']")
            .TextContent.ShouldContain("Maximum van 500 tekens bereikt.");
    }

    [Fact]
    public void RejectDescriptionLongerThan500CharactersWithoutCallingApi()
    {
        // Arrange
        var service = RegisterMorningBlock();
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='activity-description']")
            .Input(new string('a', 501));
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.Find("#activity-description-error")
            .TextContent.ShouldContain("maximaal 500 tekens");
        _ = service.DidNotReceive().CreateActivityAsync(
            Arg.Any<CreatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ShowServerDescriptionValidationError()
    {
        // Arrange
        var service = RegisterMorningBlock();
        service.CreateActivityAsync(
                Arg.Any<CreatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult(
                HttpStatusCode.BadRequest, "The request is invalid.")
            {
                FieldErrors =
                [
                    new ClientFieldError(
                        "description",
                        "Description must be 500 characters or fewer.")
                ]
            }));
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='time-block-1'] [data-testid='add-activity-button']")
            .Click();
        cut.Find("[data-testid='activity-name']").Change("Read a book");
        cut.Find("[data-testid='activity-description']")
            .Input("Bring the library card");
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#activity-description-error")
                .TextContent.ShouldContain("500 characters or fewer");
            cut.Find("[data-testid='activity-description']")
                .GetAttribute("aria-invalid").ShouldBe("true");
            cut.FindAll("[data-testid='activity-save-error']")
                .ShouldBeEmpty();
        });
    }

    private IDayPlanningClient RegisterMorningBlock()
    {
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlocks = [Block(1, "Morning", 6, 9)]
                })));
        Services.AddSingleton(service);
        return service;
    }
}
