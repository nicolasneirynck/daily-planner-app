using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planning.Services.Tests.Fixtures;
using Planning.Services.TimeBlocks;

namespace Planning.Services.Tests.TimeBlocks;

public class DefaultTimeBlockInitializerShould(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task AddSixDefaultTimeBlocksForUserWithoutBlocks()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        await using var dbContext = fixture.CreateContext();
        dbContext.Users.Add(new IdentityUser
        {
            Id = userId,
            UserName = $"user-{userId}@example.com"
        });
        await dbContext.SaveChangesAsync();

        var initializer = new DefaultTimeBlockInitializer(dbContext);

        // Act
        await initializer.EnsureForUserAsync(userId);

        // Assert
        var blocks = await dbContext.TimeBlocks.AsNoTracking()
            .Where(block => block.UserId == userId)
            .OrderBy(block => block.SortOrder)
            .ToListAsync();

        blocks.Select(block => block.SortOrder).ToArray()
            .ShouldBe([1, 2, 3, 4, 5, 6]);
        blocks.Select(block => block.Name).ToArray()
            .ShouldBe([
                "Ochtend",
                "Voormiddag",
                "Middag",
                "Vroege namiddag",
                "Late namiddag",
                "Avond"
            ]);
        blocks.Select(block => (block.StartTime, block.EndTime)).ToArray()
            .ShouldBe(new (TimeOnly StartTime, TimeOnly EndTime)[]
            {
                (new TimeOnly(6, 0), new TimeOnly(9, 0)),
                (new TimeOnly(9, 0), new TimeOnly(12, 0)),
                (new TimeOnly(12, 0), new TimeOnly(14, 0)),
                (new TimeOnly(14, 0), new TimeOnly(16, 0)),
                (new TimeOnly(16, 0), new TimeOnly(18, 0)),
                (new TimeOnly(18, 0), new TimeOnly(23, 0))
            });
    }

    [Fact]
    public async Task KeepTheSameTimeBlocksWhenInitializedAgain()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        await using var dbContext = fixture.CreateContext();
        dbContext.Users.Add(new IdentityUser
        {
            Id = userId,
            UserName = $"user-{userId}@example.com"
        });
        await dbContext.SaveChangesAsync();

        var initializer = new DefaultTimeBlockInitializer(dbContext);
        await initializer.EnsureForUserAsync(userId);

        var originalIds = await dbContext.TimeBlocks.AsNoTracking()
            .Where(block => block.UserId == userId)
            .OrderBy(block => block.SortOrder)
            .Select(block => block.Id)
            .ToArrayAsync();

        // Act
        await initializer.EnsureForUserAsync(userId);

        // Assert
        var currentIds = await dbContext.TimeBlocks.AsNoTracking()
            .Where(block => block.UserId == userId)
            .OrderBy(block => block.SortOrder)
            .Select(block => block.Id)
            .ToArrayAsync();

        currentIds.ShouldBe(originalIds);
    }
}
