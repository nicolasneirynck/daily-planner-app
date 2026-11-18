using System.Security.Claims;
using Ardalis.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.PlannedActivities;
using Planning.Domain.TimeBlocks;
using Planning.Persistence;
using Planning.Services.DayPlanning;
using Planning.Services.Tests.Fakers;
using Planning.Services.Tests.Fixtures;
using Planning.Shared.DayPlanning;

namespace Planning.Services.Tests.DayPlanning;

public class DayPlanningServiceShould(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectAnActivityWithoutAName(string? name)
    {
        var userId = Guid.NewGuid().ToString();

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var service = CreateAuthenticatedService(dbContext, userId);

        var result = await service.CreateActivityAsync(
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = blocks[0].Id,
                Name = name!
            });

        result.Status.ShouldBe(ResultStatus.Invalid);
        (await dbContext.PlannedActivities.AnyAsync(
            activity => activity.UserId == userId)).ShouldBeFalse();
    }

    [Fact]
    public async Task RejectAnotherUsersTimeBlockWithoutSavingAnActivity()
    {
        var currentUserId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();

        await using var dbContext = fixture.CreateContext();
        await AddUserWithTimeBlocksAsync(dbContext, currentUserId);
        var otherUserBlocks =
            await AddUserWithTimeBlocksAsync(dbContext, otherUserId);

        var service = CreateAuthenticatedService(dbContext, currentUserId);

        var result = await service.CreateActivityAsync(
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = otherUserBlocks[0].Id,
                Name = "Read a book"
            });

        result.Status.ShouldBe(ResultStatus.NotFound);
        (await dbContext.PlannedActivities.AnyAsync(
            activity => activity.UserId == currentUserId)).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveActivityInSelectedTimeBlockAndReturnItWhenDayIsReopened()
    {
        var userId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var service = CreateAuthenticatedService(dbContext, userId);

        var createResult = await service.CreateActivityAsync(
            new CreatePlannedActivityRequest
            {
                Date = date,
                TimeBlockId = blocks[2].Id,
                Name = "Read a book"
            });

        createResult.IsSuccess.ShouldBeTrue();

        await using var reopenedContext = fixture.CreateContext();
        var reopenedService = CreateAuthenticatedService(
            reopenedContext, userId);
        var dayResult = await reopenedService.GetDayAsync(date);

        dayResult.IsSuccess.ShouldBeTrue();
        dayResult.Value.TimeBlocks
            .Single(block => block.Id == blocks[2].Id)
            .Activities.Single().Name.ShouldBe("Read a book");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SaveCompletionStatusAndReturnItWhenDayIsReopened(
        bool initialStatus, bool updatedStatus)
    {
        var userId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var activity = new PlannedActivity
        {
            UserId = userId,
            Name = "Read a book",
            Date = date,
            TimeBlockId = blocks[0].Id,
            IsCompleted = initialStatus
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);

        var result = await service.SetActivityCompletionAsync(
            activity.Id, updatedStatus);

        result.IsSuccess.ShouldBeTrue();

        /* checken of wel wijziging wel degelijk in database staat, ipv in geheugen van de eerste dbContext
         vergelijkbaar met een gebruiker die na markeren van een activiteit de pagina herlaadt,
         dezelfde dagplanning daarmee opnieuw opent. De applicatie haalt dan de status opnieuw uit de database op*/
        await using var reopenedContext = fixture.CreateContext();
        var reopenedService = CreateAuthenticatedService(
            reopenedContext, userId);
        var day = await reopenedService.GetDayAsync(date);

        day.Value.TimeBlocks.Single(block => block.Id == blocks[0].Id)
            .Activities.Single().IsCompleted.ShouldBe(updatedStatus);
    }

    [Fact]
    public async Task RejectAnotherUsersActivityWithoutChangingItsStatus()
    {
        var currentUserId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();

        await using var dbContext = fixture.CreateContext();
        await AddUserWithTimeBlocksAsync(dbContext, currentUserId);
        var otherUserBlocks = await AddUserWithTimeBlocksAsync(
            dbContext, otherUserId);

        var activity = new PlannedActivity
        {
            UserId = otherUserId,
            Name = "Read a book",
            Date = new DateOnly(2026, 11, 14),
            TimeBlockId = otherUserBlocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, currentUserId);

        var result = await service.SetActivityCompletionAsync(activity.Id, true);

        result.Status.ShouldBe(ResultStatus.NotFound);

        await using var reopenedContext = fixture.CreateContext();
        var savedActivity = await reopenedContext.PlannedActivities
            .SingleAsync(saved => saved.Id == activity.Id);
        savedActivity.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteOnlySelectedPlannedActivity()
    {
        var userId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var activityToDelete = new PlannedActivity
        {
            UserId = userId,
            Name = "Read a book",
            Date = date,
            TimeBlockId = blocks[0].Id
        };
        var activityToKeep = new PlannedActivity
        {
            UserId = userId,
            Name = "Morning walk",
            Date = date,
            TimeBlockId = blocks[0].Id
        };
        dbContext.PlannedActivities.AddRange(activityToDelete, activityToKeep);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);
        var result = await service.DeletePlannedActivityAsync(activityToDelete.Id);

        result.IsSuccess.ShouldBeTrue();

        await using var reopenedContext = fixture.CreateContext();
        var reopenedService = CreateAuthenticatedService(reopenedContext, userId);
        var day = await reopenedService.GetDayAsync(date);

        day.IsSuccess.ShouldBeTrue();
        day.Value.TimeBlocks.SelectMany(block => block.Activities)
            .Select(activity => activity.Id).ToArray()
            .ShouldBe([activityToKeep.Id]);
        (await reopenedContext.PlannedActivities.AnyAsync(
            activity => activity.Id == activityToDelete.Id)).ShouldBeFalse();
        (await reopenedContext.PlannedActivities.SingleAsync(
            activity => activity.Id == activityToKeep.Id)).IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task RejectAnotherUsersPlannedActivityWithoutDeletingIt()
    {
        var currentUserId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();

        await using var dbContext = fixture.CreateContext();
        await AddUserWithTimeBlocksAsync(dbContext, currentUserId);
        var otherUserBlocks =
            await AddUserWithTimeBlocksAsync(dbContext, otherUserId);

        var activity = new PlannedActivity
        {
            UserId = otherUserId,
            Name = "Morning walk",
            Date = new DateOnly(2026, 11, 14),
            TimeBlockId = otherUserBlocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, currentUserId);
        var result = await service.DeletePlannedActivityAsync(activity.Id);

        result.Status.ShouldBe(ResultStatus.NotFound);

        await using var reopenedContext = fixture.CreateContext();
        (await reopenedContext.PlannedActivities.AnyAsync(
            saved => saved.Id == activity.Id && !saved.IsDeleted))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task RejectDeletingAPlannedActivityWithoutAUser()
    {
        await using var dbContext = fixture.CreateContext();
        var service = new DayPlanningService(
            dbContext,
            new FakeSessionContextProvider(new ClaimsPrincipal()));

        var result = await service.DeletePlannedActivityAsync(42);

        result.Status.ShouldBe(ResultStatus.Unauthorized);
    }

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

    private static DayPlanningService CreateAuthenticatedService(
        ApplicationDbContext dbContext, string userId)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
        return new DayPlanningService(
            dbContext, new FakeSessionContextProvider(user));
    }

    private static async Task<TimeBlock[]> AddUserWithTimeBlocksAsync(
        ApplicationDbContext dbContext, string userId)
    {
        dbContext.Users.Add(new IdentityUser
        {
            Id = userId,
            UserName = $"user-{userId}@example.com"
        });

        var blocks = Enumerable.Range(1, 6).Select(order => new TimeBlock
        {
            UserId = userId,
            Name = $"Block {order}",
            SortOrder = order,
            StartTime = new TimeOnly(order, 0),
            EndTime = new TimeOnly(order + 1, 0)
        }).ToArray();

        dbContext.TimeBlocks.AddRange(blocks);
        await dbContext.SaveChangesAsync();

        return blocks;
    }
}
