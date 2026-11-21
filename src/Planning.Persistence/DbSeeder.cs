using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.PlannedActivities;

namespace Planning.Persistence;
/// <summary>
/// Seeds the database
/// </summary>
/// <param name="dbContext"></param>
/// <param name="roleManager"></param>
/// <param name="userManager"></param>
public class DbSeeder(ApplicationDbContext dbContext, RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager)
{
    const string PasswordDefault = "A1b2C3!";
    public async Task SeedAsync()
    {
        await RolesAsync();
        await UsersAsync();
    }

    private async Task RolesAsync()
    {
        foreach (var role in new[] { "Administrator", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
    
    private async Task  UsersAsync()
    {
        if (dbContext.Users.Any())
        {
            var existingUser = await userManager.FindByEmailAsync("user@example.com");
            if (existingUser is not null && !await userManager.IsInRoleAsync(existingUser, "User"))
                await userManager.AddToRoleAsync(existingUser, "User");
            return;
        }
        
        await dbContext.Roles.ToListAsync();

        var admin = new IdentityUser
        {
            UserName = "admin@example.com",
            Email = "admin@example.com",
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(admin, PasswordDefault);
        
        var user = new IdentityUser
        {
            UserName = "user@example.com",
            Email = "user@example.com",
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(user, PasswordDefault);
        
        await userManager.AddToRoleAsync(admin, "Administrator");
        await userManager.AddToRoleAsync(user, "User");
    }

    public async Task SeedPlannedActivitiesAsync()
    {
        var user = await userManager.FindByEmailAsync("user@example.com");
        if (user is null)
            return;

        var blocks = await dbContext.TimeBlocks
            .Where(block => block.UserId == user.Id && !block.IsDeleted)
            .ToDictionaryAsync(block => block.SortOrder, block => block.Id);

        if (!Enumerable.Range(1, 6).All(blocks.ContainsKey))
            return;

        string[][] activityNames =
        [
            ["Ochtendwandeling", "Sporten", "Ontbijten", "De dag voorbereiden"],
            ["Administratie bijwerken", "Weekplanning maken", "E-mails beantwoorden", "Concentratiewerk"],
            ["Lunch met een vriend", "Lunchpauze", "Korte wandeling", "Boodschappenlijst maken"],
            ["Boodschappen doen", "Familiebezoek", "Afspraak bij de kapper", "Buiten wandelen"],
            ["Huis opruimen", "Koken", "De was doen", "Planten water geven"],
            ["Boek lezen", "Film kijken", "Spelletje spelen", "Ontspannen"]
        ];

        var today = DateOnly.FromDateTime(DateTime.Today);
        var existing = (await dbContext.PlannedActivities
            .Where(activity =>
                activity.UserId == user.Id &&
                activity.Date >= today &&
                activity.Date <= today.AddDays(6) &&
                !activity.IsDeleted)
            .Select(activity => new
            {
                activity.Date,
                activity.TimeBlockId,
                activity.Name
            })
            .ToListAsync())
            .ToLookup(activity => (activity.Date, activity.TimeBlockId));

        for (var day = 0; day < 7; day++)
        {
            var date = today.AddDays(day);
            var random = new Random(date.DayNumber);
            var counts = new[] { 0, 1, 2, 3, 4, random.Next(5) };

            for (var i = counts.Length - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                (counts[i], counts[swapIndex]) = (counts[swapIndex], counts[i]);
            }

            for (var block = 1; block <= 6; block++)
            {
                var blockId = blocks[block];
                var existingActivities = existing[(date, blockId)].ToList();
                var existingNames = existingActivities.Select(activity => activity.Name).ToHashSet();
                var namesToAdd = activityNames[block - 1]
                    .Where(name => !existingNames.Contains(name))
                    .Take(Math.Max(0, counts[block - 1] - existingActivities.Count));

                foreach (var name in namesToAdd)
                {
                    dbContext.PlannedActivities.Add(new PlannedActivity
                    {
                        UserId = user.Id,
                        Date = date,
                        TimeBlockId = blockId,
                        Name = name
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
