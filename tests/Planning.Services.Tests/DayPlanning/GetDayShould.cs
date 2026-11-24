using System.Security.Claims;
using Ardalis.Result;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.PlannedActivities;
using Planning.Services.DayPlanning;
using Planning.Services.Tests.Fakers;
using Planning.Services.Tests.Fixtures;
using static Planning.Services.Tests.DayPlanning.DayPlanningServiceTestHelper;

namespace Planning.Services.Tests.DayPlanning;

[Collection("DayPlanningService")]
public class GetDayShould(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task ReturnEveryTimeBlockWhenTheDayHasNoActivities()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        await AddUserWithTimeBlocksAsync(dbContext, userId);

        var service = CreateAuthenticatedService(dbContext, userId);

        // Act
        var result = await service.GetDayAsync(date);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Date.ShouldBe(date);
        result.Value.TimeBlocks.Select(block => block.Name).ToArray()
            .ShouldBe(["Block 1", "Block 2", "Block 3", "Block 4", "Block 5", "Block 6"]);
        result.Value.TimeBlocks.Select(block => (block.StartTime, block.EndTime)).ToArray()
            .ShouldBe(new (TimeOnly StartTime, TimeOnly EndTime)[]
            {
                (new TimeOnly(1, 0), new TimeOnly(2, 0)),
                (new TimeOnly(2, 0), new TimeOnly(3, 0)),
                (new TimeOnly(3, 0), new TimeOnly(4, 0)),
                (new TimeOnly(4, 0), new TimeOnly(5, 0)),
                (new TimeOnly(5, 0), new TimeOnly(6, 0)),
                (new TimeOnly(6, 0), new TimeOnly(7, 0))
            });
        result.Value.TimeBlocks
            .ShouldAllBe(block => block.Activities.Count == 0);
    }

    [Fact]
    public async Task ReturnActivitiesInTheirTimeBlocks()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);

        dbContext.PlannedActivities.AddRange(
            new PlannedActivity
            {
                UserId = userId,
                Name = "Morning walk",
                Date = date,
                TimeBlockId = blocks[0].Id
            },
            new PlannedActivity
            {
                UserId = userId,
                Name = "Read a book",
                Date = date,
                TimeBlockId = blocks[3].Id
            });
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);

        // Act
        var result = await service.GetDayAsync(date);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TimeBlocks.Single(block => block.SortOrder == 1)
            .Activities.Single().Name.ShouldBe("Morning walk");
        result.Value.TimeBlocks.Single(block => block.SortOrder == 4)
            .Activities.Single().Name.ShouldBe("Read a book");
    }

    [Fact]
    public async Task ReturnActivityDetailsInTheRequestedTimeBlock()
    {
        var userId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 24);

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var activity = new PlannedActivity
        {
            UserId = userId,
            Name = "Prepare presentation",
            Description = "Bring the slides",
            Date = date,
            TimeBlockId = blocks[0].Id,
            IsImportant = true,
            IsCompleted = false
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);

        var result = await service.GetDayAsync(date);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Date.ShouldBe(date);

        var block = result.Value.TimeBlocks.Single(
            item => item.Id == blocks[0].Id);
        block.Name.ShouldBe(blocks[0].Name);
        block.StartTime.ShouldBe(blocks[0].StartTime);
        block.EndTime.ShouldBe(blocks[0].EndTime);

        var returnedActivity = block.Activities.Single();
        returnedActivity.Id.ShouldBe(activity.Id);
        returnedActivity.Name.ShouldBe("Prepare presentation");
        returnedActivity.Description.ShouldBe("Bring the slides");
        returnedActivity.IsImportant.ShouldBeTrue();
        returnedActivity.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task ExcludeActivitiesFromOtherDates()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var selectedDate = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);

        dbContext.PlannedActivities.AddRange(
            new PlannedActivity
            {
                UserId = userId,
                Name = "Selected day activity",
                Date = selectedDate,
                TimeBlockId = blocks[0].Id
            },
            new PlannedActivity
            {
                UserId = userId,
                Name = "Other day activity",
                Date = selectedDate.AddDays(1),
                TimeBlockId = blocks[0].Id
            });
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);

        // Act
        var result = await service.GetDayAsync(selectedDate);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var activityNames = result.Value.TimeBlocks
            .Single(block => block.SortOrder == 1)
            .Activities.Select(activity => activity.Name).ToArray();

        activityNames.ShouldContain("Selected day activity");
        activityNames.ShouldNotContain("Other day activity");
    }

    [Fact]
    public async Task ExcludeOtherUsersTimeBlocksAndActivities()
    {
        // Arrange
        var currentUserId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        var currentUserBlocks =
            await AddUserWithTimeBlocksAsync(dbContext, currentUserId);
        var otherUserBlocks =
            await AddUserWithTimeBlocksAsync(dbContext, otherUserId);

        dbContext.PlannedActivities.AddRange(
            new PlannedActivity
            {
                UserId = currentUserId,
                Name = "Current user's activity",
                Date = date,
                TimeBlockId = currentUserBlocks[0].Id
            },
            new PlannedActivity
            {
                UserId = otherUserId,
                Name = "Other user's activity",
                Date = date,
                TimeBlockId = otherUserBlocks[0].Id
            });
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, currentUserId);

        // Act
        var result = await service.GetDayAsync(date);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TimeBlocks.Select(block => block.Id).ToArray()
            .ShouldBe(currentUserBlocks.Select(block => block.Id).ToArray());

        var activityNames = result.Value.TimeBlocks
            .SelectMany(block => block.Activities)
            .Select(activity => activity.Name).ToArray();

        activityNames.ShouldContain("Current user's activity");
        activityNames.ShouldNotContain("Other user's activity");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReturnUnauthorizedWhenUserIdIsMissingOrBlank(string? userId)
    {
        // Arrange
        await using var dbContext = fixture.CreateContext();
        Claim[] claims = userId is null
            ? []
            : [new Claim(ClaimTypes.NameIdentifier, userId)];

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var service = new DayPlanningService(
            dbContext, new FakeSessionContextProvider(user));

        // Act
        var result = await service.GetDayAsync(new DateOnly(2026, 11, 14));

        // Assert
        result.Status.ShouldBe(ResultStatus.Unauthorized);
    }
}
