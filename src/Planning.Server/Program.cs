using Destructurama;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planning.Persistence;
using Planning.Persistence.Triggers;
using Planning.Server.Errors;
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
        .AddSingleton<ApiProblemResponse>()
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

            await dbSeeder.SeedPlannedActivitiesAsync();
        }
    }
    // Theses middlewares are strict in order of calling!
    app.UseHttpsRedirection()
        .UseBlazorFrameworkFiles() // Blazor is also served from the API. 
        .UseStaticFiles()
        .UseDefaultExceptionHandler()
        .Use(async (context, next) => // onverwachte exceptions omzetten naar afgesproken API-foutcontract
        {
            try
            {
                await next(context); // "Ga verder met de aanvraag; loop de resterende ketting van middleware en endpoints maar af; als verderop een exception ontstaat, vang ik die hier op."
            }
            catch (Exception ex) when (
                context.Request.Path.StartsWithSegments("/api") && // exception enkel opvangen voor een pad op /api
                !context.Response.HasStarted) // en enkel opvangen zolang de HTTP-respons nog niet naar de client is begonnen
            {
                context.Response.Clear(); // wist wat eventueel al voor de respons was ingesteld
                /* Een onderdeel verderop kan al iets aan de respons hebben ingesteld voordat het een exception gooit, bijvoorbeeld een status of Content-Type. Als je daarna een nieuwe 500-respons schrijft, kunnen die oude gegevens ermee botsen.*/

                var writer = context.RequestServices
                    .GetRequiredService<ApiProblemResponse>();
                var problem = writer.Create(
                    context, StatusCodes.Status500InternalServerError);

                Log.Error(ex,
                    "Unhandled API exception {Instance} at {Path}",
                    problem.Instance, context.Request.Path);

                await writer.WriteAsync(
                    context, problem, context.RequestAborted); // context.RequestAborted: een annuleringssignaal voor als de client de verbinding verbreekt (u cancelation token)
            }
        })
        .UseStatusCodePages(async statusContext => // Statuscodepagina’s behandelen foutstatussen zonder geschreven inhoud. Staat vóór authenticatie en fastendpoints om hun lege 401/403 responses op te vangen
        {
            var context = statusContext.HttpContext;
            if (!context.Request.Path.StartsWithSegments("/api"))
                return;

            var writer = context.RequestServices
                .GetRequiredService<ApiProblemResponse>();

            await writer.WriteAsync(
                context,
                writer.Create(context, context.Response.StatusCode),
                context.RequestAborted);
        })
        .UseAuthentication()
        .UseAuthorization()
        .UseFastEndpoints(o =>
        {
            o.Errors.ContentType = "application/problem+json";
            o.Errors.GeneralErrorsField = "$";
            o.Errors.ProducesMetadataType = typeof(ApiProblemDetails);
            o.Errors.ResponseBuilder = (failures, context, status) =>
            {
                var errors = failures
                    .Select(failure => ApiFieldError.From(
                        failure.PropertyName, failure.ErrorMessage))
                    .ToArray();

                return context.RequestServices
                    .GetRequiredService<ApiProblemResponse>()
                    .Create(context, status, errors);
            };

            o.Endpoints.Configurator = ep =>
            {
                ep.DontAutoSendResponse();
                ep.PreProcessor<GlobalRequestLogger>(Order.Before);
                ep.PostProcessor<GlobalResponseSender>(Order.Before);
                ep.PostProcessor<GlobalResponseLogger>(Order.Before);
            };
        })
        .UseSwaggerGen();
    app.Map("/api", WriteApiNotFoundAsync);
    app.Map("/api/{**path}", WriteApiNotFoundAsync);
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

static Task WriteApiNotFoundAsync(HttpContext context)
{
    var writer = context.RequestServices
        .GetRequiredService<ApiProblemResponse>();

    return writer.WriteAsync(
        context,
        writer.Create(context, StatusCodes.Status404NotFound),
        context.RequestAborted);
}

public partial class Program { }

