using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Planning.Services.DayPlanning;

namespace Planning.Server.Tests.DayPlanning;

public sealed class DayPlanningApp : WebApplicationFactory<Program>
{
    public RecordingDayPlanningService DayPlanningService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DatabaseConnection"] =
                        "Host=localhost;Database=unused;Username=unused;Password=unused"
                }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDayPlanningService>();
            services.AddSingleton<IDayPlanningService>(DayPlanningService);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                options.DefaultChallengeScheme = TestAuthHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.Scheme, _ => { });
        });
    }
}
