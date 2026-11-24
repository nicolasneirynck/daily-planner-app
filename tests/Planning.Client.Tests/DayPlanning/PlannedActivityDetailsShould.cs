using Planning.Shared.DayPlanning;
using Shouldly;
using static Planning.Client.Pages.DayPlanningTestData;

namespace Planning.Client.DayPlanning;

public class PlannedActivityDetailsShould : BunitContext
{
    [Fact]
    public void ShowActivityDetailsWithoutEditableFields()
    {
        // Arrange
        var date = new DateOnly(2026, 11, 24);
        var morning = Block(1, "Ochtend", 6, 9);
        var activity = new PlannedActivityDto
        {
            Id = 101,
            Name = "Morning walk",
            Description = "Walk for twenty minutes",
            IsImportant = true
        };

        // Act
        var cut = Render<PlannedActivityDetails>(parameters => parameters
            .Add(component => component.Activity, activity)
            .Add(component => component.Date, date)
            .Add(component => component.TimeBlock, morning));

        // Assert
        cut.Find("[data-testid='activity-detail-name']")
            .TextContent.Trim().ShouldBe("Morning walk");
        cut.Find("[data-testid='activity-detail-date']")
            .TextContent.Trim().ShouldBe("Dinsdag 24 november 2026");
        cut.Find("[data-testid='activity-detail-time-block']")
            .TextContent.Trim().ShouldBe("Ochtend");
        cut.Find("[data-testid='activity-detail-importance']")
            .TextContent.Trim().ShouldBe("Belangrijk");
        cut.Find("[data-testid='activity-detail-description']")
            .TextContent.Trim().ShouldBe("Walk for twenty minutes");
        cut.FindAll("input, textarea, select, form").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, false, false, false)]
    [InlineData("   ", false, false, false)]
    [InlineData("Bring water", false, true, false)]
    [InlineData(null, true, false, true)]
    public void ShowOnlyAvailableOptionalDetails(
        string? description,
        bool isImportant,
        bool showDescription,
        bool showImportance)
    {
        // Arrange
        var activity = new PlannedActivityDto
        {
            Id = 101,
            Name = "Morning walk",
            Description = description,
            IsImportant = isImportant
        };

        // Act
        var cut = Render<PlannedActivityDetails>(parameters => parameters
            .Add(component => component.Activity, activity)
            .Add(component => component.Date, new DateOnly(2026, 11, 24))
            .Add(component => component.TimeBlock, Block(1, "Ochtend", 6, 9)));

        // Assert
        cut.FindAll("[data-testid='activity-detail-description']")
            .Count.ShouldBe(showDescription ? 1 : 0);
        cut.FindAll("[data-testid='activity-detail-importance']")
            .Count.ShouldBe(showImportance ? 1 : 0);
    }

    [Fact]
    public async Task RequestEditingWhenEditButtonIsClicked()
    {
        // Arrange
        var editRequested = false;
        var activity = new PlannedActivityDto
        {
            Id = 101,
            Name = "Morning walk"
        };

        var cut = Render<PlannedActivityDetails>(parameters => parameters
            .Add(component => component.Activity, activity)
            .Add(component => component.Date, new DateOnly(2026, 11, 24))
            .Add(component => component.TimeBlock, Block(1, "Ochtend", 6, 9))
            .Add(component => component.OnEdit, () => editRequested = true));

        // Act
        var editButton = cut.Find("[data-testid='activity-detail-edit-button']");
        await editButton.ClickAsync();

        // Assert
        editButton.GetAttribute("aria-label").ShouldBe("Bewerken");
        editRequested.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, "Activiteit voltooien")]
    [InlineData(true, "Activiteit voltooid")]
    public async Task RequestCompletionChangeFromTheStatusButton(
        bool isCompleted,
        string expectedLabel)
    {
        // Arrange
        var activity = new PlannedActivityDto
        {
            Id = 101,
            Name = "Morning walk",
            IsCompleted = isCompleted
        };
        PlannedActivityDto? requestedActivity = null;

        var cut = Render<PlannedActivityDetails>(parameters => parameters
            .Add(component => component.Activity, activity)
            .Add(component => component.Date, new DateOnly(2026, 11, 24))
            .Add(component => component.TimeBlock, Block(1, "Ochtend", 6, 9))
            .Add(component => component.OnToggleCompletion,
                (PlannedActivityDto selected) => requestedActivity = selected));

        // Act
        var button = cut.Find(
            "button[data-testid='activity-detail-completion-toggle']");
        await button.ClickAsync();

        // Assert
        button.TextContent.Trim().ShouldBe(expectedLabel);
        button.GetAttribute("aria-pressed")
            .ShouldBe(isCompleted ? "true" : "false");
        requestedActivity.ShouldBeSameAs(activity);
    }

    [Fact]
    public void ShowCompletionErrorAndKeepActionAvailable()
    {
        // Arrange
        var activity = new PlannedActivityDto
        {
            Id = 101,
            Name = "Morning walk",
            IsCompleted = false
        };

        // Act
        var cut = Render<PlannedActivityDetails>(parameters => parameters
            .Add(component => component.Activity, activity)
            .Add(component => component.Date, new DateOnly(2026, 11, 24))
            .Add(component => component.TimeBlock, Block(1, "Ochtend", 6, 9))
            .Add(component => component.CompletionError, true));

        // Assert
        cut.Find("[role='alert']").TextContent
            .ShouldContain("Status wijzigen is niet gelukt.");

        var button = cut.Find(
            "[data-testid='activity-detail-completion-toggle']");
        button.GetAttribute("aria-pressed").ShouldBe("false");
        button.GetAttribute("disabled").ShouldBeNull();
    }

    [Fact]
    public async Task RequestDeletionWhenDeleteButtonIsClicked()
    {
        // Arrange
        var deleteRequested = false;
        var activity = new PlannedActivityDto
        {
            Id = 101,
            Name = "Morning walk"
        };

        var cut = Render<PlannedActivityDetails>(parameters => parameters
            .Add(component => component.Activity, activity)
            .Add(component => component.Date, new DateOnly(2026, 11, 24))
            .Add(component => component.TimeBlock, Block(1, "Ochtend", 6, 9))
            .Add(component => component.OnDelete,
                () => deleteRequested = true));

        // Act
        var button = cut.Find(
            "button[data-testid='activity-detail-delete-button']");
        await button.ClickAsync();

        // Assert
        button.TextContent.Trim().ShouldBe("Verwijderen uit planning");
        deleteRequested.ShouldBeTrue();
    }
}
