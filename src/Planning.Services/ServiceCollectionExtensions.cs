using Microsoft.Extensions.DependencyInjection;
using Planning.Persistence;
using Planning.Services.DayPlanning;
using Planning.Services.TimeBlocks;

namespace Planning.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IDayPlanningService, DayPlanningService>();
        services.AddScoped<DefaultTimeBlockInitializer>();
        services.AddTransient<DbSeeder>();       
        
        // Add other application services here.
        return services;
    }
}
