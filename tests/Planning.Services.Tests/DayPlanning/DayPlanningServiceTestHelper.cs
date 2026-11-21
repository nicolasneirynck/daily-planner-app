using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Planning.Domain.TimeBlocks;
using Planning.Persistence;
using Planning.Services.DayPlanning;
using Planning.Services.Tests.Fakers;

namespace Planning.Services.Tests.DayPlanning;

internal static class DayPlanningServiceTestHelper
{
    internal static DayPlanningService CreateAuthenticatedService(
        ApplicationDbContext dbContext, string userId)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
        return new DayPlanningService(
            dbContext, new FakeSessionContextProvider(user));
    }

    internal static async Task<TimeBlock[]> AddUserWithTimeBlocksAsync(
        ApplicationDbContext dbContext, string userId)
    {
        dbContext.Users.Add(new IdentityUser
        {
            Id = userId,
            UserName = $"user-{userId}@example.com"
        });

        var blocks = Enumerable.Range(1, 6).Select(order => new TimeBlock
        {
            UserId = userId,
            Name = $"Block {order}",
            SortOrder = order,
            StartTime = new TimeOnly(order, 0),
            EndTime = new TimeOnly(order + 1, 0)
        }).ToArray();

        dbContext.TimeBlocks.AddRange(blocks);
        await dbContext.SaveChangesAsync();

        return blocks;
    }
}
