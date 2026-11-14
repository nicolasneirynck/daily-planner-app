using Microsoft.EntityFrameworkCore;
using Planning.Domain.TimeBlocks;
using Planning.Persistence;

namespace Planning.Services.TimeBlocks;

public class DefaultTimeBlockInitializer(ApplicationDbContext dbContext)
{
    private static readonly (int SortOrder, string Name)[] Defaults =
    [
        (1, "Ochtend"),
        (2, "Voormiddag"),
        (3, "Vroege namiddag"),
        (4, "Late namiddag"),
        (5, "Avond")
    ];

    public async Task EnsureForUserAsync(
        string userId, CancellationToken ctx = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var existingBlocks = await dbContext.TimeBlocks
            .Where(block => block.UserId == userId)
            .ToDictionaryAsync(block => block.SortOrder, ctx);

        foreach (var (sortOrder, name) in Defaults)
        {
            if (existingBlocks.TryGetValue(sortOrder, out var block))
            {
                block.Name = name;
                block.IsDeleted = false;
            }
            else
            {
                dbContext.TimeBlocks.Add(new TimeBlock
                {
                    UserId = userId,
                    Name = name,
                    SortOrder = sortOrder
                });
            }
        }

        await dbContext.SaveChangesAsync(ctx);
    }
}
