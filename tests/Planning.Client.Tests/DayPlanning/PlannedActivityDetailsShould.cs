using Bunit;
using Planning.Client.DayPlanning;
using Planning.Shared.DayPlanning;
using Shouldly;

namespace Planning.Client.Tests.DayPlanning;

public class PlannedActivityDetailsShould : BunitContext
{
    [Fact]
    public void ShowCompleteActivityDetailsWithoutInputFields()
    {
        // Arrange
        var activity = new PlannedActivityDetailsDto
        {
            Id = 101,
            Name = "Morning walk",
            Description = "Bring a jacket",
            Date = new DateOnly(2026, 11, 24),
            TimeBlockId = 1,
            TimeBlockName = "Ochtend",
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(9, 0),
            IsImportant = true
        };

        // Act
        var cut = Render<PlannedActivityDetails>(parameters => parameters
            .Add(component => component.Activity, activity));

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
            .TextContent.Trim().ShouldBe("Bring a jacket");
        cut.FindAll("input, textarea, select").ShouldBeEmpty();
    }

    [Fact]
    public void HideOptionalSectionsWhenActivityHasNoDescriptionOrImportance()
    {
        // Arrange
        var activity = new PlannedActivityDetailsDto
        {
            Id = 101,
            Name = "Morning walk",
            Date = new DateOnly(2026, 11, 24),
            TimeBlockId = 1,
            TimeBlockName = "Ochtend",
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(9, 0)
        };

        // Act
        var cut = Render<PlannedActivityDetails>(parameters => parameters
            .Add(component => component.Activity, activity));

        // Assert
        cut.Find("[data-testid='activity-detail-name']")
            .TextContent.Trim().ShouldBe("Morning walk");
        cut.Find("[data-testid='activity-detail-date']")
            .TextContent.Trim().ShouldBe("Dinsdag 24 november 2026");
        cut.Find("[data-testid='activity-detail-time-block']")
            .TextContent.Trim().ShouldBe("Ochtend");
        cut.FindAll("[data-testid='activity-detail-importance']")
            .ShouldBeEmpty();
        cut.FindAll("[data-testid='activity-detail-description']")
            .ShouldBeEmpty();
    }
}
