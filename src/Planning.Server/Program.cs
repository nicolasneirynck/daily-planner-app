using Destructurama;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planning.Persistence;
using Planning.Persistence.Triggers;
using Planning.Server.Identity;
using Planning.Server.Processors;
using Planning.Services;
using Planning.Services.Identity;
using Planning.Services.TimeBlocks;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger(); // Initial log setup, will be overwritten by Serilog, but we need a logger before Dependency Injection is activated.

try
{
    Log.Information("Starting web application");
    var builder = WebApplication.CreateBuilder(args);

    builder.Services
        .AddSerilog((_, lc) => lc.ReadFrom.Configuration(builder.Configuration) // Configuration in AppSettings.json
            .Destructure.UsingAttributes()) // Sensitive data logging
        .AddIdentity<IdentityUser, IdentityRole>() 
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .Services.AddDbContext<ApplicationDbContext>(o =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DatabaseConnection") ??
                                   throw new InvalidOperationException("Connection string 'DatabaseConnection' not found.");
            o.UseNpgsql(connectionString); // PostgreSQL, run locally with docker compose (see Readme.md).
            o.EnableDetailedErrors();
            if (builder.Environment.IsDevelopment())
            {
                o.EnableSensitiveDataLogging(); // only enabled in development.
            }
            o.UseTriggers(options => options.AddTrigger<EntityBeforeSaveTrigger>()); // Handles all UpdatedAt, CreatedAt stuff.
        })
        .AddHttpContextAccessor()
        .AddScoped<ISessionContextProvider, HttpContextSessionProvider>() // Provides the current user from the HttpContext to the session provider.
        .AddApplicationServices() // You'll need to add your own services in this function call.
        .AddAuthorization()
        .AddFastEndpoints(o =>
        {
            o.IncludeAbstractValidators = true; // Include validators from abstract classes (see https://docs.fluentvalidation.net/en/latest/).
            o.Assemblies = [typeof(Planning.Shared.Products.ProductRequest).Assembly]; // Adds the validators from other assemblies
        })
        .SwaggerDocument(o =>
        {
            o.DocumentSettings = s =>
            {
                s.Title = "Planning API";
            };
        });

    var app = builder.Build();
    // apply Database migraticons on startup, not so wise in production (Use Generated SQL Scripts) 
    // See: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli
    if (app.Environment.IsDevelopment())
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbSeeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
            // dbContext.Database.EnsureDeleted(); // Delete the database if it exists to clean it up if needed.

            dbContext.Database.Migrate(); // Creates the database if it doesn't exist and applies all migrations. See Readme.md for more info.
            await dbSeeder.SeedAsync(); // Seeds the database with some test data.

            // Development accounts are seeded after migrations; remove this when seeding creates their blocks or is retired.
            var initializer = scope.ServiceProvider.GetRequiredService<DefaultTimeBlockInitializer>();
            var userIds = await dbContext.Users.AsNoTracking()
                .Select(user => user.Id)
                .ToListAsync();

            foreach (var userId in userIds)
                await initializer.EnsureForUserAsync(userId);
        }
    }
    // Theses middlewares are strict in order of calling!
    app.UseHttpsRedirection()
        .UseBlazorFrameworkFiles() // Blazor is also served from the API. 
        .UseStaticFiles()
        .UseDefaultExceptionHandler()
        .UseAuthentication()
        .UseAuthorization()
        .UseFastEndpoints(o =>
        {
            o.Endpoints.Configurator = ep =>
            {
                ep.DontAutoSendResponse();
                ep.PreProcessor<GlobalRequestLogger>(Order.Before);
                ep.PostProcessor<GlobalResponseSender>(Order.Before);
                ep.PostProcessor<GlobalResponseLogger>(Order.Before);
            };
        })
        .UseSwaggerGen();
    app.MapFallbackToFile("index.html"); // Serves the Blazor app from the API, when no routes match.
    app.Run();
}
catch (HostAbortedException)
{
    // EF Core stops the host after obtaining its services at design time.
}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occured during bootstrapping");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }

