using Microsoft.Extensions.DependencyInjection;
using Planning.Persistence;
using Planning.Services.DayPlanning;
using Planning.Services.Products;
using Planning.Services.Projects;
using Planning.Services.TimeBlocks;
using Planning.Shared.DayPlanning;
using Planning.Shared.Products;
using Planning.Shared.Projects;

namespace Planning.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IDayPlanningService, DayPlanningService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<DefaultTimeBlockInitializer>();
        services.AddTransient<DbSeeder>();       
        
        // Add other application services here.
        return services;
    }
}
