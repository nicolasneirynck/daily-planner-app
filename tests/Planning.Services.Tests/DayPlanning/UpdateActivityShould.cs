using System.Security.Claims;
using Ardalis.Result;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.PlannedActivities;
using Planning.Services.DayPlanning;
using Planning.Services.Tests.Fakers;
using Planning.Services.Tests.Fixtures;
using Planning.Shared.DayPlanning;
using static Planning.Services.Tests.DayPlanning.DayPlanningServiceTestHelper;

namespace Planning.Services.Tests.DayPlanning;

[Collection("DayPlanningService")]
public class UpdateActivityShould(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task SaveChangedNameAndReturnItWhenDayIsReopened()
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
            TimeBlockId = blocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);
        var result = await service.UpdateActivityAsync(
            activity.Id,
            new UpdatePlannedActivityRequest
            {
                Date = date,
                TimeBlockId = blocks[0].Id,
                Name = "Read two chapters",
                IsImportant = false
            });

        result.IsSuccess.ShouldBeTrue();

        await using var reopenedContext = fixture.CreateContext();
        var reopenedService = CreateAuthenticatedService(reopenedContext, userId);
        var day = await reopenedService.GetDayAsync(date);

        day.IsSuccess.ShouldBeTrue();
        day.Value.TimeBlocks.Single(block => block.Id == blocks[0].Id)
            .Activities.Single().Name.ShouldBe("Read two chapters");
    }

    [Theory]
    [InlineData(null, "Bring notes")]
    [InlineData("Old description", "New description")]
    [InlineData("Old description", null)]
    public async Task SaveAddedChangedOrRemovedDescription(
        string? initialDescription, string? updatedDescription)
    {
        var userId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var activity = new PlannedActivity
        {
            UserId = userId,
            Name = "Read a book",
            Description = initialDescription,
            Date = date,
            TimeBlockId = blocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);
        var result = await service.UpdateActivityAsync(
            activity.Id,
            new UpdatePlannedActivityRequest
            {
                Date = date,
                TimeBlockId = blocks[0].Id,
                Name = activity.Name,
                Description = updatedDescription,
                IsImportant = false
            });

        result.IsSuccess.ShouldBeTrue();

        await using var reopenedContext = fixture.CreateContext();
        var reopenedService = CreateAuthenticatedService(reopenedContext, userId);
        var day = await reopenedService.GetDayAsync(date);

        day.IsSuccess.ShouldBeTrue();
        day.Value.TimeBlocks.Single(block => block.Id == blocks[0].Id)
            .Activities.Single().Description.ShouldBe(updatedDescription);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SaveChangedImportanceAndReturnItWhenDayIsReopened(
        bool initialImportance, bool updatedImportance)
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
            IsImportant = initialImportance
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);
        var result = await service.UpdateActivityAsync(
            activity.Id,
            new UpdatePlannedActivityRequest
            {
                Date = date,
                TimeBlockId = blocks[0].Id,
                Name = activity.Name,
                IsImportant = updatedImportance
            });

        result.IsSuccess.ShouldBeTrue();

        await using var reopenedContext = fixture.CreateContext();
        var reopenedService = CreateAuthenticatedService(reopenedContext, userId);
        var day = await reopenedService.GetDayAsync(date);

        day.IsSuccess.ShouldBeTrue();
        day.Value.TimeBlocks.Single(block => block.Id == blocks[0].Id)
            .Activities.Single().IsImportant.ShouldBe(updatedImportance);
    }

    [Fact]
    public async Task MoveActivityToAnotherDateAndReturnItWhenDayIsReopened()
    {
        var userId = Guid.NewGuid().ToString();
        var originalDate = new DateOnly(2026, 11, 14);
        var newDate = new DateOnly(2026, 11, 15);

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var activity = new PlannedActivity
        {
            UserId = userId,
            Name = "Read a book",
            Date = originalDate,
            TimeBlockId = blocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);
        var result = await service.UpdateActivityAsync(
            activity.Id,
            new UpdatePlannedActivityRequest
            {
                Date = newDate,
                TimeBlockId = blocks[0].Id,
                Name = activity.Name,
                IsImportant = false
            });

        result.IsSuccess.ShouldBeTrue();

        await using var reopenedContext = fixture.CreateContext();
        var reopenedService = CreateAuthenticatedService(reopenedContext, userId);
        var originalDay = await reopenedService.GetDayAsync(originalDate);
        var newDay = await reopenedService.GetDayAsync(newDate);

        originalDay.IsSuccess.ShouldBeTrue();
        newDay.IsSuccess.ShouldBeTrue();
        originalDay.Value.TimeBlocks.SelectMany(block => block.Activities)
            .ShouldBeEmpty();
        newDay.Value.TimeBlocks.Single(block => block.Id == blocks[0].Id)
            .Activities.Single().Id.ShouldBe(activity.Id);
    }

    [Fact]
    public async Task MoveActivityToAnotherTimeBlockAndReturnItWhenDayIsReopened()
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
            TimeBlockId = blocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);
        var result = await service.UpdateActivityAsync(
            activity.Id,
            new UpdatePlannedActivityRequest
            {
                Date = date,
                TimeBlockId = blocks[1].Id,
                Name = activity.Name,
                IsImportant = false
            });

        result.IsSuccess.ShouldBeTrue();

        await using var reopenedContext = fixture.CreateContext();
        var reopenedService = CreateAuthenticatedService(reopenedContext, userId);
        var day = await reopenedService.GetDayAsync(date);

        day.IsSuccess.ShouldBeTrue();
        day.Value.TimeBlocks.Single(block => block.Id == blocks[0].Id)
            .Activities.ShouldBeEmpty();
        day.Value.TimeBlocks.Single(block => block.Id == blocks[1].Id)
            .Activities.Single().Id.ShouldBe(activity.Id);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, true)]
    public async Task RejectInvalidNameWithoutChangingActivity(
        string? name, bool isTooLong)
    {
        var userId = Guid.NewGuid().ToString();
        var originalDate = new DateOnly(2026, 11, 14);
        var updatedName = isTooLong
            ? new string('a', PlannedActivityRules.MaxNameLength + 1)
            : name;

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var activity = new PlannedActivity
        {
            UserId = userId,
            Name = "Read a book",
            Date = originalDate,
            TimeBlockId = blocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);
        var result = await service.UpdateActivityAsync(
            activity.Id,
            new UpdatePlannedActivityRequest
            {
                Date = originalDate.AddDays(1),
                TimeBlockId = blocks[1].Id,
                Name = updatedName!,
                Description = "Changed description",
                IsImportant = true
            });

        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ValidationErrors.Single();
        error.Identifier.ShouldBe("Name");
        error.ErrorMessage.ShouldBe(isTooLong
            ? PlannedActivityRules.NameTooLongMessage
            : PlannedActivityRules.NameRequiredMessage);

        await using var reopenedContext = fixture.CreateContext();
        var savedActivity = await reopenedContext.PlannedActivities
            .SingleAsync(saved => saved.Id == activity.Id);

        savedActivity.Name.ShouldBe("Read a book");
        savedActivity.Description.ShouldBeNull();
        savedActivity.Date.ShouldBe(originalDate);
        savedActivity.TimeBlockId.ShouldBe(blocks[0].Id);
        savedActivity.IsImportant.ShouldBeFalse();
    }

    [Fact]
    public async Task RejectDescriptionLongerThan500CharactersWithoutChangingActivity()
    {
        var userId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var activity = new PlannedActivity
        {
            UserId = userId,
            Name = "Read a book",
            Description = "Bring notes",
            Date = date,
            TimeBlockId = blocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, userId);
        var result = await service.UpdateActivityAsync(
            activity.Id,
            new UpdatePlannedActivityRequest
            {
                Date = date,
                TimeBlockId = blocks[0].Id,
                Name = activity.Name,
                Description = new string(
                    'a', PlannedActivityRules.MaxDescriptionLength + 1),
                IsImportant = false
            });

        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ValidationErrors.Single();
        error.Identifier.ShouldBe("Description");
        error.ErrorMessage.ShouldBe(
            PlannedActivityRules.DescriptionTooLongMessage);

        await using var reopenedContext = fixture.CreateContext();
        var savedActivity = await reopenedContext.PlannedActivities
            .SingleAsync(saved => saved.Id == activity.Id);
        savedActivity.Description.ShouldBe("Bring notes");
    }

    [Fact]
    public async Task RejectAnotherUsersActivityWithoutChangingIt()
    {
        var currentUserId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        var currentUserBlocks =
            await AddUserWithTimeBlocksAsync(dbContext, currentUserId);
        var otherUserBlocks =
            await AddUserWithTimeBlocksAsync(dbContext, otherUserId);
        var activity = new PlannedActivity
        {
            UserId = otherUserId,
            Name = "Read a book",
            Date = date,
            TimeBlockId = otherUserBlocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, currentUserId);
        var result = await service.UpdateActivityAsync(
            activity.Id,
            new UpdatePlannedActivityRequest
            {
                Date = date,
                TimeBlockId = currentUserBlocks[0].Id,
                Name = "Changed name",
                IsImportant = true
            });

        result.Status.ShouldBe(ResultStatus.NotFound);

        await using var reopenedContext = fixture.CreateContext();
        var savedActivity = await reopenedContext.PlannedActivities
            .SingleAsync(saved => saved.Id == activity.Id);
        savedActivity.Name.ShouldBe("Read a book");
        savedActivity.TimeBlockId.ShouldBe(otherUserBlocks[0].Id);
        savedActivity.IsImportant.ShouldBeFalse();
    }

    [Fact]
    public async Task RejectAnotherUsersTimeBlockWithoutChangingActivity()
    {
        var currentUserId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 11, 14);

        await using var dbContext = fixture.CreateContext();
        var currentUserBlocks =
            await AddUserWithTimeBlocksAsync(dbContext, currentUserId);
        var otherUserBlocks =
            await AddUserWithTimeBlocksAsync(dbContext, otherUserId);
        var activity = new PlannedActivity
        {
            UserId = currentUserId,
            Name = "Read a book",
            Date = date,
            TimeBlockId = currentUserBlocks[0].Id
        };
        dbContext.PlannedActivities.Add(activity);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthenticatedService(dbContext, currentUserId);
        var result = await service.UpdateActivityAsync(
            activity.Id,
            new UpdatePlannedActivityRequest
            {
                Date = date.AddDays(1),
                TimeBlockId = otherUserBlocks[0].Id,
                Name = "Changed name",
                IsImportant = true
            });

        result.Status.ShouldBe(ResultStatus.NotFound);

        await using var reopenedContext = fixture.CreateContext();
        var savedActivity = await reopenedContext.PlannedActivities
            .SingleAsync(saved => saved.Id == activity.Id);
        savedActivity.Name.ShouldBe("Read a book");
        savedActivity.Date.ShouldBe(date);
        savedActivity.TimeBlockId.ShouldBe(currentUserBlocks[0].Id);
        savedActivity.IsImportant.ShouldBeFalse();
    }

    [Fact]
    public async Task RejectUpdatingAnActivityWithoutAUser()
    {
        await using var dbContext = fixture.CreateContext();
        var service = new DayPlanningService(
            dbContext,
            new FakeSessionContextProvider(new ClaimsPrincipal()));

        var result = await service.UpdateActivityAsync(
            42,
            new UpdatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 1,
                Name = "Read a book",
                IsImportant = false
            });

        result.Status.ShouldBe(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task RejectUpdatingAnActivityThatDoesNotExist()
    {
        var userId = Guid.NewGuid().ToString();

        await using var dbContext = fixture.CreateContext();
        var blocks = await AddUserWithTimeBlocksAsync(dbContext, userId);
        var service = CreateAuthenticatedService(dbContext, userId);

        var result = await service.UpdateActivityAsync(
            int.MaxValue,
            new UpdatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = blocks[0].Id,
                Name = "Read a book",
                IsImportant = false
            });

        result.Status.ShouldBe(ResultStatus.NotFound);
    }
}
