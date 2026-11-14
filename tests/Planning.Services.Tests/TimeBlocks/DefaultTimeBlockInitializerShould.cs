using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planning.Services.Tests.Fixtures;
using Planning.Services.TimeBlocks;

namespace Planning.Services.Tests.TimeBlocks;

public class DefaultTimeBlockInitializerShould(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task AddFiveDefaultTimeBlocksForUserWithoutBlocks()
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
            .ShouldBe([1, 2, 3, 4, 5]);
        blocks.Select(block => block.Name).ToArray()
            .ShouldBe([
                "Ochtend",
                "Voormiddag",
                "Vroege namiddag",
                "Late namiddag",
                "Avond"
            ]);
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
