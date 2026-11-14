using Microsoft.EntityFrameworkCore;
using Planning.Domain.TimeBlocks;
using Planning.Persistence;

namespace Planning.Services.TimeBlocks;

public class DefaultTimeBlockInitializer(ApplicationDbContext dbContext)
{
    private static readonly (int SortOrder, string Name, TimeOnly StartTime, TimeOnly EndTime)[] Defaults =
    [
        (1, "Ochtend", new TimeOnly(6, 0), new TimeOnly(9, 0)),
        (2, "Voormiddag", new TimeOnly(9, 0), new TimeOnly(12, 0)),
        (3, "Middag", new TimeOnly(12, 0), new TimeOnly(14, 0)),
        (4, "Vroege namiddag", new TimeOnly(14, 0), new TimeOnly(16, 0)),
        (5, "Late namiddag", new TimeOnly(16, 0), new TimeOnly(18, 0)),
        (6, "Avond", new TimeOnly(18, 0), new TimeOnly(23, 0))
    ];

    public async Task EnsureForUserAsync(
        string userId, CancellationToken ctx = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var existingBlocks = await dbContext.TimeBlocks
            .Where(block => block.UserId == userId)
            .ToDictionaryAsync(block => block.SortOrder, ctx);

        foreach (var (sortOrder, name, startTime, endTime) in Defaults)
        {
            if (existingBlocks.TryGetValue(sortOrder, out var block))
            {
                block.Name = name;
                block.StartTime = startTime;
                block.EndTime = endTime;
                block.IsDeleted = false;
            }
            else
            {
                dbContext.TimeBlocks.Add(new TimeBlock
                {
                    UserId = userId,
                    Name = name,
                    SortOrder = sortOrder,
                    StartTime = startTime,
                    EndTime = endTime
                });
            }
        }

        await dbContext.SaveChangesAsync(ctx);
    }
}
