using System.Net;
using AngleSharp.Html.Dom;
using NSubstitute;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.Pages;

public class PlannedActivityEditingShould : BunitContext
{
    public PlannedActivityEditingShould()
    {
        JSInterop.SetupModule("./js/activityDialog.js")
            .SetupVoid("showModal", _ => true);
    }

    [Fact]
    public void OpenSelectedActivityForEditingWithPrefilledDetails()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var morning = Block(1, "Ochtend", 6, 9);
        morning.Activities =
        [
            new PlannedActivityDto
            {
                Id = 101,
                Name = "Morning walk",
                Description = "Walk for twenty minutes",
                IsImportant = true
            }
        ];

        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = today,
                    TimeBlocks = [morning, Block(2, "Middag", 12, 14)]
                })));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();

        // Assert
        var dialog = cut.Find("[role='dialog']");
        dialog.TextContent.ShouldContain("Activiteit aanpassen");

        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Morning walk");
        cut.Find("[data-testid='activity-description']")
            .GetAttribute("value").ShouldBe("Walk for twenty minutes");
        ((IHtmlInputElement)cut.Find("[data-testid='activity-date']"))
            .Value.ShouldBe(today.ToString("yyyy-MM-dd"));
        ((IHtmlSelectElement)cut.Find("[data-testid='activity-time-block']"))
            .Value.ShouldBe("1");
        ((IHtmlInputElement)cut.Find("[data-testid='activity-important']"))
            .IsChecked.ShouldBeTrue();

        cut.Find("[data-testid='update-activity-button']")
            .TextContent.ShouldContain("Wijzigingen opslaan");

        var saveButton = cut.Find("[data-testid='update-activity-button']");
        saveButton.GetAttribute("disabled").ShouldNotBeNull();

        cut.Find("[data-testid='activity-name']").Input("Evening walk");
        cut.Find("[data-testid='update-activity-button']")
            .GetAttribute("disabled").ShouldBeNull();

        cut.Find("[data-testid='activity-name']").Input("Morning walk");
        cut.Find("[data-testid='update-activity-button']")
            .GetAttribute("disabled").ShouldNotBeNull();
    }

    [Fact]
    public void UpdateSelectedActivityNameAndShowItInDayPlanning()
    {
        // Arrange
        var storedName = "Morning walk";
        var service = Substitute.For<IDayPlanningClient>();

        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var morning = Block(1, "Ochtend", 6, 9);
                morning.Activities =
                [
                    new PlannedActivityDto
                    {
                        Id = 101,
                        Name = storedName
                    }
                ];

                return Task.FromResult(new ClientResult<DayPlanningResponse>(
                    HttpStatusCode.OK,
                    new DayPlanningResponse
                    {
                        Date = call.ArgAt<DateOnly>(0),
                        TimeBlocks = [morning]
                    }));
            });

        service.UpdateActivityAsync(
                101,
                Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                storedName = call.ArgAt<UpdatePlannedActivityRequest>(1).Name;
                return Task.FromResult(new ClientResult(HttpStatusCode.NoContent));
            });

        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Evening walk");
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).UpdateActivityAsync(
                101,
                Arg.Is<UpdatePlannedActivityRequest>(request =>
                    request.Name == "Evening walk"),
                Arg.Any<CancellationToken>());

            cut.FindAll("[role='dialog']").ShouldBeEmpty();
            cut.Find("[data-testid='planned-activity-name']")
                .TextContent.ShouldBe("Evening walk");
        });
    }

    [Theory]
    [InlineData(null, "Bring water", "Bring water")]
    [InlineData("Bring water", "Bring a jacket", "Bring a jacket")]
    [InlineData("Bring water", "", null)]
    public void UpdateDescriptionAndShowItWhenReopened(
        string? originalDescription,
        string editedDescription,
        string? expectedDescription)
    {
        // Arrange
        var storedDescription = originalDescription;
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var morning = Block(1, "Ochtend", 6, 9);
                morning.Activities =
                [
                    new PlannedActivityDto
                    {
                        Id = 101,
                        Name = "Morning walk",
                        Description = storedDescription
                    }
                ];

                return Task.FromResult(new ClientResult<DayPlanningResponse>(
                    HttpStatusCode.OK,
                    new DayPlanningResponse
                    {
                        Date = call.ArgAt<DateOnly>(0),
                        TimeBlocks = [morning]
                    }));
            });
        service.UpdateActivityAsync(
                101,
                Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                storedDescription = call.ArgAt<UpdatePlannedActivityRequest>(1).Description;
                return Task.FromResult(new ClientResult(HttpStatusCode.NoContent));
            });
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-description']").Change(editedDescription);
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).UpdateActivityAsync(
                101,
                Arg.Is<UpdatePlannedActivityRequest>(request =>
                    request.Description == expectedDescription),
                Arg.Any<CancellationToken>());

            cut.FindAll("[role='dialog']").ShouldBeEmpty();
        });

        // Act: reopen the saved activity
        cut.Find("[data-testid='activity-open-button']").Click();

        // Assert
        ((IHtmlTextAreaElement)cut.Find("[data-testid='activity-description']"))
            .Value.ShouldBe(expectedDescription ?? string.Empty);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void UpdateImportanceAndShowItInDayPlanning(
        bool originalImportance,
        bool expectedImportance)
    {
        // Arrange
        var storedImportance = originalImportance;
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var morning = Block(1, "Ochtend", 6, 9);
                morning.Activities =
                [
                    new PlannedActivityDto
                    {
                        Id = 101,
                        Name = "Morning walk",
                        IsImportant = storedImportance
                    }
                ];

                return Task.FromResult(new ClientResult<DayPlanningResponse>(
                    HttpStatusCode.OK,
                    new DayPlanningResponse
                    {
                        Date = call.ArgAt<DateOnly>(0),
                        TimeBlocks = [morning]
                    }));
            });
        service.UpdateActivityAsync(
                101,
                Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                storedImportance = call.ArgAt<UpdatePlannedActivityRequest>(1).IsImportant;
                return Task.FromResult(new ClientResult(HttpStatusCode.NoContent));
            });
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-important']").Change(expectedImportance);
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).UpdateActivityAsync(
                101,
                Arg.Is<UpdatePlannedActivityRequest>(request =>
                    request.IsImportant == expectedImportance),
                Arg.Any<CancellationToken>());

            cut.FindAll("[role='dialog']").ShouldBeEmpty();
            cut.FindAll("[data-testid='activity-important-flag']")
                .Count.ShouldBe(expectedImportance ? 1 : 0);
        });
    }

    [Fact]
    public void UpdateDateAndShowActivityOnNewDay()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);
        var storedDate = today;
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var requestedDate = call.ArgAt<DateOnly>(0);
                var morning = Block(1, "Ochtend", 6, 9);
                if (requestedDate == storedDate)
                {
                    morning.Activities =
                    [
                        new PlannedActivityDto
                        {
                            Id = 101,
                            Name = "Morning walk"
                        }
                    ];
                }

                return Task.FromResult(new ClientResult<DayPlanningResponse>(
                    HttpStatusCode.OK,
                    new DayPlanningResponse
                    {
                        Date = requestedDate,
                        TimeBlocks = [morning]
                    }));
            });
        service.UpdateActivityAsync(
                101,
                Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                storedDate = call.ArgAt<UpdatePlannedActivityRequest>(1).Date;
                return Task.FromResult(new ClientResult(HttpStatusCode.NoContent));
            });
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-date']")
            .Change(tomorrow.ToString("yyyy-MM-dd"));
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).UpdateActivityAsync(
                101,
                Arg.Is<UpdatePlannedActivityRequest>(request =>
                    request.Date == tomorrow),
                Arg.Any<CancellationToken>());

            _ = service.Received(1).GetDayAsync(
                tomorrow, Arg.Any<CancellationToken>());
            cut.Find("[data-testid='planned-activity-name']")
                .TextContent.ShouldBe("Morning walk");
        });
    }

    [Fact]
    public void UpdateTimeBlockAndShowActivityInNewBlock()
    {
        // Arrange
        var storedTimeBlockId = 1;
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var morning = Block(1, "Ochtend", 6, 9);
                var midday = Block(2, "Middag", 12, 14);
                var targetBlock = storedTimeBlockId == 1 ? morning : midday;
                targetBlock.Activities =
                [
                    new PlannedActivityDto
                    {
                        Id = 101,
                        Name = "Morning walk"
                    }
                ];

                return Task.FromResult(new ClientResult<DayPlanningResponse>(
                    HttpStatusCode.OK,
                    new DayPlanningResponse
                    {
                        Date = call.ArgAt<DateOnly>(0),
                        TimeBlocks = [morning, midday]
                    }));
            });
        service.UpdateActivityAsync(
                101,
                Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                storedTimeBlockId = call.ArgAt<UpdatePlannedActivityRequest>(1).TimeBlockId;
                return Task.FromResult(new ClientResult(HttpStatusCode.NoContent));
            });
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-time-block']").Change("2");
        cut.Find("[role='dialog'] form").Submit();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _ = service.Received(1).UpdateActivityAsync(
                101,
                Arg.Is<UpdatePlannedActivityRequest>(request =>
                    request.TimeBlockId == 2),
                Arg.Any<CancellationToken>());

            cut.FindAll("[data-testid='time-block-1'] [data-testid='planned-activity-row']")
                .ShouldBeEmpty();
            cut.Find("[data-testid='time-block-2'] [data-testid='planned-activity-name']")
                .TextContent.ShouldBe("Morning walk");
        });
    }

    [Fact]
    public void KeepActivityUnchangedWhenEditingIsCancelled()
    {
        // Arrange
        var morning = Block(1, "Ochtend", 6, 9);
        morning.Activities =
        [
            new PlannedActivityDto
            {
                Id = 101,
                Name = "Morning walk"
            }
        ];

        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlocks = [morning]
                })));
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Evening walk");
        cut.Find("[role='dialog'] button[aria-label='Sluiten']").Click();

        // Assert
        cut.FindAll("[role='dialog']").ShouldBeEmpty();
        cut.Find("[data-testid='planned-activity-name']")
            .TextContent.ShouldBe("Morning walk");
        _ = service.DidNotReceive().UpdateActivityAsync(
            Arg.Any<int>(),
            Arg.Any<UpdatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());

        // Act: reopen the activity
        cut.Find("[data-testid='activity-open-button']").Click();

        // Assert
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Morning walk");
    }

    [Fact]
    public async Task ShowSavingStateAndAllowRetryAfterFailure()
    {
        // Arrange
        var morning = Block(1, "Ochtend", 6, 9);
        morning.Activities =
        [
            new PlannedActivityDto { Id = 101, Name = "Morning walk" }
        ];

        var pendingUpdate = new TaskCompletionSource<ClientResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Substitute.For<IDayPlanningClient>();
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse
                {
                    Date = DateOnly.FromDateTime(DateTime.Today),
                    TimeBlocks = [morning]
                })));
        service.UpdateActivityAsync(
                101,
                Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(pendingUpdate.Task);
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Evening walk");
        var submitTask = cut.Find("[role='dialog'] form").SubmitAsync();

        // Assert: saving is in progress
        await cut.WaitForAssertionAsync(() =>
        {
            var saveButton = cut.Find("[data-testid='update-activity-button']");
            saveButton.TextContent.ShouldBe("Opslaan...");
            saveButton.GetAttribute("disabled").ShouldNotBeNull();
        });

        // Act: the request fails
        pendingUpdate.SetResult(new ClientResult(
            HttpStatusCode.InternalServerError, "Save failed."));
        await submitTask;

        // Assert: the form stays available for another attempt
        await cut.WaitForAssertionAsync(() =>
        {
            var saveButton = cut.Find("[data-testid='update-activity-button']");
            saveButton.TextContent.ShouldContain("Wijzigingen opslaan");
            saveButton.GetAttribute("disabled").ShouldBeNull();
            cut.Find("[data-testid='activity-save-error']")
                .TextContent.ShouldContain("Opslaan is niet gelukt.");
        });
    }
}
