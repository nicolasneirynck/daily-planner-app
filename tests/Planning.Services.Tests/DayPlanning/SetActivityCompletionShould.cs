using Ardalis.Result;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.PlannedActivities;
using Planning.Services.Tests.Fixtures;
using static Planning.Services.Tests.DayPlanning.DayPlanningServiceTestHelper;

namespace Planning.Services.Tests.DayPlanning;

[Collection("DayPlanningService")]
public class SetActivityCompletionShould(PostgreSqlFixture fixture)
{
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
}
