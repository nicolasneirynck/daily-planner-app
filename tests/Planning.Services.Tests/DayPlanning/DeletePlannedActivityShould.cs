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
public class DeletePlannedActivityShould(PostgreSqlFixture fixture)
{
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
}
