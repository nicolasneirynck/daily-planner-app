using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planning.Domain.PlannedActivities;
using Planning.Persistence;
using Planning.Services.TimeBlocks;

namespace Planning.E2E.Tests;

public static class DayPlanningTestData
{
    public static async Task EnsureTimeBlocksAsync(
        E2ETestEnvironment environment) // omgeving maakt tijdelijke database en gebruiker aan
    {
        await using var scope = environment.CreateScope(); // geeft test tijdelijk toegang tot de services van dezelfde server die de browser straks bezoekt.
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var userId = await dbContext.Users
            .Where(user => user.Email == E2ETestEnvironment.UserEmail)
            .Select(user => user.Id)
            .SingleAsync();

        var initializer = scope.ServiceProvider
            .GetRequiredService<DefaultTimeBlockInitializer>();
        await initializer.EnsureForUserAsync(userId);

        // daarna kan Playwright in de browser inloggen, activiteit invullen en opslaan. De brower en de voorbereiding
        // gebruiken dezelfde tijdelijke database. Dus de browser ziet de tijdsblokkken die de test zojuist heeft aangemaakt

        // await using -> ruimt aan het einde de tijdelijke scope en databasecontext op. Verwijdert de aangemaakte tijdsblokken niet
        // die blijven beschikbaar tot de tijdelijke database na de testomgeving wordt opgeruimd
    }

    public static async Task SeedMorningActivityAsync(
        E2ETestEnvironment environment,
        string activityName,
        DateOnly date)
    {
        await EnsureTimeBlocksAsync(environment);

        await using var scope = environment.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var userId = await dbContext.Users
            .Where(user => user.Email == E2ETestEnvironment.UserEmail)
            .Select(user => user.Id)
            .SingleAsync();

        var morningBlockId = await dbContext.TimeBlocks
            .Where(block =>
                block.UserId == userId &&
                block.Name == "Ochtend" &&
                !block.IsDeleted)
            .Select(block => block.Id)
            .SingleAsync();

        dbContext.PlannedActivities.Add(new PlannedActivity
        {
            UserId = userId,
            Name = activityName,
            Date = date,
            TimeBlockId = morningBlockId
        });

        await dbContext.SaveChangesAsync();
    }
}
