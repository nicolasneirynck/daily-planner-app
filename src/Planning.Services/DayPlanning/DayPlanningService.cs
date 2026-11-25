using Microsoft.EntityFrameworkCore;
using Planning.Domain.PlannedActivities;
using Planning.Persistence;
using Planning.Services.Identity;
using Planning.Shared.DayPlanning;
using Planning.Shared.Identity;

namespace Planning.Services.DayPlanning;

public class DayPlanningService(
    ApplicationDbContext dbContext,
    ISessionContextProvider sessionContextProvider) : IDayPlanningService
{
    public async Task<Result<DayPlanningResponse>> GetDayAsync(
        DateOnly date, CancellationToken ctx = default)
    {
        var userId = sessionContextProvider.User?.GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Result<DayPlanningResponse>.Unauthorized();

        var timeBlocks = await dbContext.TimeBlocks
            .AsNoTracking() // zegt tegen Entity Framework: houd de opgehaalde entiteiten niet bij voor latere wijzigingen. Past bij deze GET die alleen gegevens leest.
            .Where(block => block.UserId == userId && !block.IsDeleted)
            .OrderBy(block => block.SortOrder)
            .Select(block => new TimeBlockDto
            {
                Id = block.Id,
                Name = block.Name,
                SortOrder = block.SortOrder,
                StartTime = block.StartTime,
                EndTime = block.EndTime
            })
            .ToListAsync(ctx);

        var plannedActivities = await dbContext.PlannedActivities
            .AsNoTracking()
            .Where(activity =>
                activity.UserId == userId &&
                activity.Date == date &&
                !activity.IsDeleted)
            .OrderBy(activity => activity.Id)
            .Select(activity => new
            {
                activity.TimeBlockId,
                activity.Id,
                activity.Name,
                activity.Description,
                activity.IsCompleted,
                activity.IsImportant
            })
            .ToListAsync(ctx);

        /* ToLookup groepeert de geplande activiteiten op TimeBlockId. Als blok 2 bijvoorbeeld twee activiteiten heeft, geeft activitiesByBlock[2] die twee terug. Heeft blok 3 er geen, dan geeft activitiesByBlock[3] een lege verzameling terug.*/
        var activitiesByBlock = plannedActivities.ToLookup(
            activity => activity.TimeBlockId);

        foreach (var block in timeBlocks)
        {
            block.Activities = activitiesByBlock[block.Id]
                .Select(activity => new PlannedActivityDto
                {
                    Id = activity.Id,
                    Name = activity.Name,
                    Description = activity.Description,
                    IsCompleted = activity.IsCompleted,
                    IsImportant = activity.IsImportant
                })
                .ToArray();
        }

        return Result.Success(new DayPlanningResponse
        {
            Date = date,
            TimeBlocks = timeBlocks
        });
    }

    public async Task<Result<PlannedActivityDetailsDto>> GetActivityAsync(
        int activityId, CancellationToken ctx = default)
    {
        var userId = sessionContextProvider.User?.GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Result<PlannedActivityDetailsDto>.Unauthorized();

        var activity = await dbContext.PlannedActivities
            .AsNoTracking()
            .Where(activity => // in SQL eerst join en dan where, maar hier is het leesbaarder om omgekeerd te schrijven. Achter de schermen gebeurt eerst de join.
                activity.Id == activityId &&
                activity.UserId == userId &&
                !activity.IsDeleted)
            .Join( // JOIN "TimeBlocks" AS timeBlock ON activity."TimeBlockId" = timeBlock."Id" AND activity."UserId" = timeBlock."UserId"
                dbContext.TimeBlocks.AsNoTracking(),
                activity => new { activity.TimeBlockId, activity.UserId },
                timeBlock => new { TimeBlockId = timeBlock.Id, timeBlock.UserId },
                (activity, timeBlock) => new PlannedActivityDetailsDto
                {
                    Id = activity.Id,
                    Name = activity.Name,
                    Description = activity.Description,
                    Date = activity.Date,
                    TimeBlockId = timeBlock.Id,
                    TimeBlockName = timeBlock.Name,
                    StartTime = timeBlock.StartTime,
                    EndTime = timeBlock.EndTime,
                    IsImportant = activity.IsImportant,
                    IsCompleted = activity.IsCompleted
                })
            .SingleOrDefaultAsync(ctx); // null, PlannedActivityDetailsDto of fout (bij meer dan 1 resultaat)

        return activity is null
            ? Result.NotFound("Planned activity was not found.")
            : Result.Success(activity);
    }

    public async Task<Result<PlannedActivityDto>> CreateActivityAsync(
        CreatePlannedActivityRequest request, CancellationToken ctx = default)
    {
        var userId = sessionContextProvider.User?.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Name),
                ErrorMessage = PlannedActivityRules.NameRequiredMessage
            });

        if (request.Name.Length > PlannedActivityRules.MaxNameLength)
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Name),
                ErrorMessage = PlannedActivityRules.NameTooLongMessage
            });

        if (request.Description?.Length > PlannedActivityRules.MaxDescriptionLength)
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Description),
                ErrorMessage = PlannedActivityRules.DescriptionTooLongMessage
            });

        var blockExists = await dbContext.TimeBlocks.AnyAsync(block =>
            block.Id == request.TimeBlockId &&
            block.UserId == userId &&
            !block.IsDeleted, ctx);

        if (!blockExists)
            return Result.NotFound("Time block was not found.");

        var activity = new PlannedActivity
        {
            UserId = userId,
            Date = request.Date,
            TimeBlockId = request.TimeBlockId,
            Name = request.Name,
            Description = request.Description,
            IsImportant = request.IsImportant
        };
        dbContext.PlannedActivities.Add(activity);

        await dbContext.SaveChangesAsync(ctx);
        return Result.Success(new PlannedActivityDto
        {
            Id = activity.Id,
            Name = activity.Name,
            Description = activity.Description,
            IsCompleted = activity.IsCompleted,
            IsImportant = activity.IsImportant
        });
    }

    public async Task<Result> UpdateActivityAsync(
        int activityId, UpdatePlannedActivityRequest request,
        CancellationToken ctx = default)
    {
        var userId = sessionContextProvider.User?.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Unauthorized();

        var activity = await dbContext.PlannedActivities.SingleOrDefaultAsync(
            activity => activity.Id == activityId &&
                        activity.UserId == userId &&
                        !activity.IsDeleted,
            ctx);

        if (activity is null)
            return Result.NotFound("Planned activity was not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Name),
                ErrorMessage = PlannedActivityRules.NameRequiredMessage
            });

        if (request.Name.Length > PlannedActivityRules.MaxNameLength)
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Name),
                ErrorMessage = PlannedActivityRules.NameTooLongMessage
            });

        if (request.Description?.Length > PlannedActivityRules.MaxDescriptionLength)
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Description),
                ErrorMessage = PlannedActivityRules.DescriptionTooLongMessage
            });

        var blockExists = await dbContext.TimeBlocks.AnyAsync(block =>
            block.Id == request.TimeBlockId &&
            block.UserId == userId &&
            !block.IsDeleted, ctx);

        if (!blockExists)
            return Result.NotFound("Time block was not found.");

        activity.Name = request.Name;
        activity.Description = request.Description;
        activity.IsImportant = request.IsImportant;
        activity.Date = request.Date;
        activity.TimeBlockId = request.TimeBlockId;

        await dbContext.SaveChangesAsync(ctx);
        return Result.Success();
    }

    public async Task<Result> SetActivityCompletionAsync(
        int activityId, bool isCompleted, CancellationToken ctx = default)
    {
        var userId = sessionContextProvider.User?.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Unauthorized();

        var activity = await dbContext.PlannedActivities.SingleOrDefaultAsync(
            activity => activity.Id == activityId &&
                        activity.UserId == userId &&
                        !activity.IsDeleted,
            ctx);

        if (activity is null)
            return Result.NotFound("Planned activity was not found.");

        activity.IsCompleted = isCompleted;
        await dbContext.SaveChangesAsync(ctx);
        return Result.Success();
    }

    public async Task<Result> DeletePlannedActivityAsync(
        int plannedActivityId, CancellationToken ctx = default)
    {
        var userId = sessionContextProvider.User?.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Unauthorized();

        var deletedCount = await dbContext.PlannedActivities
            .Where(activity =>
                activity.Id == plannedActivityId &&
                activity.UserId == userId &&
                !activity.IsDeleted)
            .ExecuteDeleteAsync(ctx);

        return deletedCount == 0
            ? Result.NotFound("Planned activity was not found.")
            : Result.Success();
    }
}
