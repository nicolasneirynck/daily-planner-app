using System.Security.Claims;
using Ardalis.Result;
using Planning.Domain.PlannedActivities;
using Planning.Services.DayPlanning;
using Planning.Services.Tests.Fakers;
using Planning.Services.Tests.Fixtures;
using static Planning.Services.Tests.DayPlanning.DayPlanningServiceTestHelper;

namespace Planning.Services.Tests.DayPlanning;

[Collection("DayPlanningService")]
public class GetActivityShould(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task ReturnTheSavedDetailsOfAnOwnedActivity()
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
            IsCompleted = true
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);

        var result = await service.GetActivityAsync(activity.Id);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(activity.Id);
        result.Value.Name.ShouldBe("Prepare presentation");
        result.Value.Description.ShouldBe("Bring the slides");
        result.Value.Date.ShouldBe(date);
        result.Value.TimeBlockId.ShouldBe(blocks[0].Id);
        result.Value.TimeBlockName.ShouldBe(blocks[0].Name);
        result.Value.StartTime.ShouldBe(blocks[0].StartTime);
        result.Value.EndTime.ShouldBe(blocks[0].EndTime);
        result.Value.IsImportant.ShouldBeTrue();
        result.Value.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnNotFoundWhenActivityDoesNotExist()
    {
        var userId = Guid.NewGuid().ToString();

        await using var dbContext = fixture.CreateContext();
        await AddUserWithTimeBlocksAsync(dbContext, userId);
        var service = CreateAuthenticatedService(dbContext, userId);

        var result = await service.GetActivityAsync(int.MaxValue);

        result.Status.ShouldBe(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ReturnNotFoundWhenActivityIsDeleted()
    {
        var userId = Guid.NewGuid().ToString();

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var activity = new PlannedActivity
        {
            UserId = userId,
            Name = "Read a book",
            Date = new DateOnly(2026, 11, 24),
            TimeBlockId = blocks[0].Id,
            IsDeleted = true
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);
        var result = await service.GetActivityAsync(activity.Id);

        result.Status.ShouldBe(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ReturnNotFoundForAnotherUsersActivity()
    {
        var currentUserId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();

        await using var dbContext = fixture.CreateContext();
        await AddUserWithTimeBlocksAsync(dbContext, currentUserId);
        var otherUsersBlocks = await AddUserWithTimeBlocksAsync(
            dbContext, otherUserId);
        var activity = new PlannedActivity
        {
            UserId = otherUserId,
            Name = "Private activity",
            Date = new DateOnly(2026, 11, 24),
            TimeBlockId = otherUsersBlocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, currentUserId);
        var result = await service.GetActivityAsync(activity.Id);

        result.Status.ShouldBe(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ReturnUnauthorizedWithoutAUser()
    {
        await using var dbContext = fixture.CreateContext();
        var service = new DayPlanningService(
            dbContext,
            new FakeSessionContextProvider(new ClaimsPrincipal()));

        var result = await service.GetActivityAsync(42);

        result.Status.ShouldBe(ResultStatus.Unauthorized);
    }
}
