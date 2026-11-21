using Ardalis.Result;
using Microsoft.EntityFrameworkCore;
using Planning.Services.Tests.Fixtures;
using Planning.Shared.DayPlanning;
using static Planning.Services.Tests.DayPlanning.DayPlanningServiceTestHelper;

namespace Planning.Services.Tests.DayPlanning;

[Collection("DayPlanningService")]
public class CreateActivityShould(PostgreSqlFixture fixture)
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
        var error = result.ValidationErrors.Single();
        error.Identifier.ShouldBe("Name");
        error.ErrorMessage.ShouldBe("Activity name is required.");
        (await dbContext.PlannedActivities.AnyAsync(
            activity => activity.UserId == userId)).ShouldBeFalse();
    }

    [Fact]
    public async Task RejectAnActivityWithANameLongerThan250Characters()
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
                Name = new string('a', 251)
            });

        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ValidationErrors.Single();
        error.Identifier.ShouldBe("Name");
        error.ErrorMessage.ShouldBe("Activity name must be 250 characters or fewer.");
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
        createResult.Value.Description.ShouldBeNull();

        await using var reopenedContext = fixture.CreateContext();
        var reopenedService = CreateAuthenticatedService(
            reopenedContext, userId);
        var dayResult = await reopenedService.GetDayAsync(date);

        dayResult.IsSuccess.ShouldBeTrue();
        dayResult.Value.TimeBlocks
            .Single(block => block.Id == blocks[2].Id)
            .Activities.Single().Name.ShouldBe("Read a book");
        dayResult.Value.TimeBlocks
            .Single(block => block.Id == blocks[2].Id)
            .Activities.Single().Description.ShouldBeNull();
    }

    [Fact]
    public async Task SaveDescriptionAndReturnItWhenDayIsReopened()
    {
        var userId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);
        var description = new string('a', 500);

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var service = CreateAuthenticatedService(dbContext, userId);

        var createResult = await service.CreateActivityAsync(
            new CreatePlannedActivityRequest
            {
                Date = date,
                TimeBlockId = blocks[2].Id,
                Name = "Read a book",
                Description = description
            });

        createResult.IsSuccess.ShouldBeTrue();
        createResult.Value.Description.ShouldBe(description);

        await using var reopenedContext = fixture.CreateContext();
        var reopenedService = CreateAuthenticatedService(reopenedContext, userId);
        var dayResult = await reopenedService.GetDayAsync(date);

        dayResult.IsSuccess.ShouldBeTrue();
        dayResult.Value.TimeBlocks
            .Single(block => block.Id == blocks[2].Id)
            .Activities.Single().Description.ShouldBe(description);
    }

    [Fact]
    public async Task RejectAnActivityWithADescriptionLongerThan500Characters()
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
                Name = "Read a book",
                Description = new string('a', 501)
            });

        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ValidationErrors.Single();
        error.Identifier.ShouldBe("Description");
        error.ErrorMessage.ShouldBe("Description must be 500 characters or fewer.");
        (await dbContext.PlannedActivities.AnyAsync(activity => activity.UserId == userId)).ShouldBeFalse();
    }
}
