using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planning.Persistence;
using Planning.Shared.Identity;
using Testcontainers.PostgreSql;
using Xunit;

namespace Planning.E2E.Tests;

public sealed class E2ETestEnvironment : IAsyncLifetime
{
    public const string UserEmail = "e2e-user@example.com";
    public const string UserPassword = "A1b2C3!";

    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgres:18").Build();
    private E2ETestApp? _app;

    private string ConnectionString => _database.GetConnectionString();
    public Uri BaseAddress { get; private set; } = default!;

    /* Deze methode doet drie dingen:
       1. Ze controleert of de testserver al is gestart. Zonder server zijn er nog geen services beschikbaar.
       2. Ze maakt een nieuwe scope: een tijdelijke gebruiksruimte voor services zoals ApplicationDbContext.
       3. Ze geeft die scope terug aan de test. De methode ruimt hem dus niet zelf op; de test moet dat doen met "await using"*/
    public AsyncServiceScope CreateScope()
    {
        if (_app is null)
            throw new InvalidOperationException("The E2E app has not started.");

        return _app.Services.CreateAsyncScope();
    }

    public async Task InitializeAsync()
    {
        // start de tijdelijke PostgreSQL-db
        await _database.StartAsync();

        // geeft connection string van die db aan de testserver -> in E2ETestApp.cs wordt ze ingesteld als "DatabaseConnection"
        _app = new E2ETestApp(ConnectionString);
        // gebruik kestrel (start een echte HTTP-server die een browser zoals playwright kan gebruiken
        // + gebruik poort 0 -> laat het systeem een vrije poort kiezen
        _app.UseKestrel(0);

        // start de testserver
        using var client = _app.CreateClient();
        BaseAddress = client.BaseAddress // bewaart adres testserver -> of gooi exception als client.BaseAddress null is
            ?? throw new InvalidOperationException(
                "The E2E server did not provide an address.");

        // vraagt aan diezelfde testserver om zijn ApplicationDbContext
        // CreateAsyncScope() -> maakt een tijdelijke ruimte voor scoped services
        // de dbcontext wordt binnen die scope opgehaald en bij het verlaten opgeruimd
        await using var scope = _app.Services.CreateAsyncScope(); // waarom vanuit _app.Services? -> dan krijgt de migratie dezelfde databasecontext en connection string als de testserver. De migratie bereidt dus precies de database voor die de applicatie gebruikt
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        // RoleManager ophalen uit de testserver -> ASP.NET Identity-service waarmee je rollen kan beheren
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();
        var roleResult = await roleManager.CreateAsync(
            new IdentityRole(AppRoles.User));
        RequireSuccess(roleResult, "create the User role");

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser
        {
            UserName = UserEmail,
            Email = UserEmail,
            EmailConfirmed = true
        };

        RequireSuccess(
            await userManager.CreateAsync(user, UserPassword),
            "create the E2E user");
        RequireSuccess(
            await userManager.AddToRoleAsync(user, AppRoles.User),
            "assign the User role");


    }

    public async Task DisposeAsync()
    {
        _app?.Dispose();
        await _database.DisposeAsync();
    }

    private static void RequireSuccess(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: " +
                string.Join(", ", result.Errors.Select(error => error.Description)));
        }
    }
}
