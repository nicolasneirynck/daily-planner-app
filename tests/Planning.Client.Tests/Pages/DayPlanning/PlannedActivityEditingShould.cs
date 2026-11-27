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
                Name = "Morning walk"
            }
        ];

        var service = Substitute.For<IDayPlanningClient>();
        service.GetActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                HttpStatusCode.OK,
                new PlannedActivityDetailsDto
                {
                    Id = 101,
                    Name = "Morning walk",
                    Description = "Walk for twenty minutes",
                    IsImportant = true,
                    Date = today,
                    TimeBlockId = 1,
                    TimeBlockName = "Ochtend",
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(9, 0)
                })));
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

        // Act: open the activity
        var mobileUrl = cut.Find("[data-testid='mobile-activity-open-link']")
            .GetAttribute("href");
        cut.Find("[data-testid='activity-open-button']").Click();

        // Assert: details appear before the edit form
        mobileUrl.ShouldBe(
            $"/dayplanning/activities/101?date={today:yyyy-MM-dd}");
        cut.Find("[role='dialog']").TextContent
            .ShouldContain("Activiteit bekijken");
        cut.Find("[data-testid='activity-detail-description']").TextContent
            .ShouldContain("Walk for twenty minutes");
        cut.Find("[data-testid='activity-detail-importance']").TextContent
            .ShouldContain("Belangrijk");
        cut.Find("[data-testid='activity-detail-time-block']").TextContent
            .Trim().ShouldBe("Ochtend");
        cut.FindAll("[role='dialog'] input, [role='dialog'] textarea, [role='dialog'] select")
            .ShouldBeEmpty();
        _ = service.Received(1).GetActivityAsync(
            101, Arg.Any<CancellationToken>());

        // Act: open the existing edit form
        cut.Find("[data-testid='activity-detail-edit-button']").Click();

        // Assert: the existing edit form is prefilled
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
    public void UpdateSelectedActivityNameAndShowItInDetailsAndDayPlanning()
    {
        // Arrange
        var storedName = "Morning walk";
        var service = Substitute.For<IDayPlanningClient>();
        ConfigureActivityDetails(service,
            () => MorningWalkDetails(name: storedName));

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
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
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

            cut.Find("[role='dialog']").TextContent
                .ShouldContain("Activiteit bekijken");
            cut.Find("[data-testid='activity-detail-name']")
                .TextContent.ShouldBe("Evening walk");
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
        ConfigureActivityDetails(service,
            () => MorningWalkDetails(description: storedDescription));
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
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
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

            cut.Find("[role='dialog']").TextContent
                .ShouldContain("Activiteit bekijken");
            if (expectedDescription is null)
                cut.FindAll("[data-testid='activity-detail-description']").ShouldBeEmpty();
            else
                cut.Find("[data-testid='activity-detail-description']")
                    .TextContent.Trim().ShouldBe(expectedDescription);
        });

        // Act: edit the saved activity again
        cut.Find("[data-testid='activity-detail-edit-button']").Click();

        // Assert
        (cut.Find("[data-testid='activity-description']")
            .GetAttribute("value") ?? string.Empty)
            .ShouldBe(expectedDescription ?? string.Empty);
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
        ConfigureActivityDetails(service,
            () => MorningWalkDetails(isImportant: storedImportance));
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
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
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

            cut.Find("[role='dialog']").TextContent
                .ShouldContain("Activiteit bekijken");
            cut.FindAll("[data-testid='activity-detail-importance']")
                .Count.ShouldBe(expectedImportance ? 1 : 0);
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
        ConfigureActivityDetails(service,
            () => MorningWalkDetails(date: storedDate));
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
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
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
            cut.Find("[role='dialog']").TextContent
                .ShouldContain("Activiteit bekijken");
            cut.Find("[data-testid='activity-detail-date']")
                .TextContent.ShouldContain(tomorrow.ToString("d MMMM yyyy",
                    System.Globalization.CultureInfo.GetCultureInfo("nl-BE")));
        });
    }

    [Fact]
    public void UpdateTimeBlockAndShowActivityInNewBlock()
    {
        // Arrange
        var storedTimeBlockId = 1;
        var service = Substitute.For<IDayPlanningClient>();
        ConfigureActivityDetails(service,
            () => MorningWalkDetails(timeBlockId: storedTimeBlockId));
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
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
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
            cut.Find("[role='dialog']").TextContent
                .ShouldContain("Activiteit bekijken");
            cut.Find("[data-testid='activity-detail-time-block']")
                .TextContent.Trim().ShouldBe("Middag");
        });
    }

    [Fact]
    public void DiscardEditedActivityChangesAfterWarning()
    {
        // Arrange
        var service = RegisterPlanningWithEditableActivity();
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Evening walk");
        cut.Find("[role='dialog'] .activity-form-cancel").Click();

        // Assert: closing requires confirmation and preserves the edited value
        cut.Find("[role='alertdialog']").TextContent
            .ShouldContain("Niet-opgeslagen wijzigingen");
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Evening walk");
        _ = service.DidNotReceive().UpdateActivityAsync(
            Arg.Any<int>(),
            Arg.Any<UpdatePlannedActivityRequest>(),
            Arg.Any<CancellationToken>());

        // Act: discard the changes
        cut.Find("[data-testid='discard-changes-button']").Click();

        // Assert: the original activity is shown in the details dialog
        cut.Find("[role='dialog']").TextContent
            .ShouldContain("Activiteit bekijken");
        cut.FindAll("[role='alertdialog']").ShouldBeEmpty();
        cut.Find("[data-testid='activity-detail-name']")
            .TextContent.ShouldBe("Morning walk");
        cut.Find("[data-testid='planned-activity-name']")
            .TextContent.ShouldBe("Morning walk");

        // Act: edit the activity again
        cut.Find("[data-testid='activity-detail-edit-button']").Click();

        // Assert
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Morning walk");
    }

    [Fact]
    public void ReturnToDetailsFromUnchangedEditedActivityWithoutWarning()
    {
        // Arrange
        RegisterPlanningWithEditableActivity();
        var cut = Render<DayPlanning>();

        // Act
        cut.Find("[data-testid='activity-open-button']").Click();
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
        cut.Find("[role='dialog'] button[aria-label='Sluiten']").Click();

        // Assert
        cut.Find("[role='dialog']").TextContent
            .ShouldContain("Activiteit bekijken");
        cut.FindAll("[role='alertdialog']").ShouldBeEmpty();
    }

    [Fact]
    public async Task KeepOtherEditOpenWhenPendingSaveFinishes()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var morning = Block(1, "Ochtend", 6, 9);
        morning.Activities =
        [
            new PlannedActivityDto { Id = 101, Name = "Morning walk" },
            new PlannedActivityDto { Id = 102, Name = "Read a book" }
        ];
        var pendingSave = new TaskCompletionSource<ClientResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Substitute.For<IDayPlanningClient>();
        service.GetActivityAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.ArgAt<int>(0);
                return Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                    HttpStatusCode.OK,
                    new PlannedActivityDetailsDto
                    {
                        Id = id,
                        Name = id == 101 ? "Morning walk" : "Read a book",
                        Date = today,
                        TimeBlockId = 1,
                        TimeBlockName = "Ochtend",
                        StartTime = new TimeOnly(6, 0),
                        EndTime = new TimeOnly(9, 0)
                    }));
            });
        service.GetDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ClientResult<DayPlanningResponse>(
                HttpStatusCode.OK,
                new DayPlanningResponse { Date = today, TimeBlocks = [morning] })));
        service.UpdateActivityAsync(101, Arg.Any<UpdatePlannedActivityRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(pendingSave.Task);
        Services.AddSingleton(service);
        var cut = Render<DayPlanning>();

        // Act
        cut.FindAll("[data-testid='activity-open-button']")[0].Click();
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
        cut.Find("[data-testid='activity-name']").Change("Evening walk");
        var saveTask = cut.Find("[role='dialog'] form").SubmitAsync();
        cut.Find(".activity-form-cancel").Click();
        cut.Find("[data-testid='discard-changes-button']").Click();
        cut.Find(".activity-details-close").Click();
        cut.FindAll("[data-testid='activity-open-button']")[1].Click();
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
        cut.Find("[data-testid='activity-name']").Input("Updated book");
        pendingSave.SetResult(new ClientResult(HttpStatusCode.NoContent));
        await saveTask;

        // Assert
        ((IHtmlInputElement)cut.Find("[data-testid='activity-name']"))
            .Value.ShouldBe("Updated book");
        _ = service.Received(1).UpdateActivityAsync(101,
            Arg.Any<UpdatePlannedActivityRequest>(), Arg.Any<CancellationToken>());
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
        ConfigureActivityDetails(service, () => MorningWalkDetails());
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
        cut.Find("[data-testid='activity-detail-edit-button']").Click();
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

    private IDayPlanningClient RegisterPlanningWithEditableActivity()
    {
        var morning = Block(1, "Ochtend", 6, 9);
        morning.Activities =
        [
            new PlannedActivityDto { Id = 101, Name = "Morning walk" }
        ];

        var service = Substitute.For<IDayPlanningClient>();
        ConfigureActivityDetails(service, () => MorningWalkDetails());
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

    private static void ConfigureActivityDetails(
        IDayPlanningClient service,
        Func<PlannedActivityDetailsDto> createDetails)
    {
        service.GetActivityAsync(101, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new ClientResult<PlannedActivityDetailsDto>(
                HttpStatusCode.OK, createDetails())));
    }

    private static PlannedActivityDetailsDto MorningWalkDetails(
        string name = "Morning walk",
        string? description = null,
        bool isImportant = false,
        DateOnly? date = null,
        int timeBlockId = 1)
    {
        var block = timeBlockId == 1
            ? Block(1, "Ochtend", 6, 9)
            : Block(2, "Middag", 12, 14);

        return new PlannedActivityDetailsDto
        {
            Id = 101,
            Name = name,
            Description = description,
            IsImportant = isImportant,
            Date = date ?? DateOnly.FromDateTime(DateTime.Today),
            TimeBlockId = block.Id,
            TimeBlockName = block.Name,
            StartTime = block.StartTime,
            EndTime = block.EndTime
        };
    }
}
