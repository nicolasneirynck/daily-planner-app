using Microsoft.EntityFrameworkCore;
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
            .AsNoTracking()
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
                activity.IsCompleted
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
                    IsCompleted = activity.IsCompleted
                })
                .ToArray();
        }

        return Result.Success(new DayPlanningResponse
        {
            Date = date,
            TimeBlocks = timeBlocks
        });
    }
}
