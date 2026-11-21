using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Planning.Client;
using Planning.Client.DayPlanning;
using Planning.Client.Identity;
using Planning.Client.Products;
using Planning.Shared.Products;

try
{
    var builder = WebAssemblyHostBuilder.CreateDefault(args);

    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
    

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.BrowserConsole(outputTemplate:"[{Timestamp:HH:mm:ss}{Level:u3}]{Message:lj} {NewLine}{Exception}")
        .CreateLogger();
    
    Log.Information("Starting web application");

// register the cookie handler
    builder.Services.AddTransient<CookieHandler>();

// set up authorization
    builder.Services.AddAuthorizationCore();

// register the custom state provider
    builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();
// register the account management interface
    builder.Services.AddScoped(sp => (IAccountManager)sp.GetRequiredService<AuthenticationStateProvider>());

    // HTTP-client voor aanvragen naar de backend.
    // CookieHandler stelt "credentials: include" in, zodat de browser
    // toepasselijke cookies kan meesturen en bv een bij login door de server uitgegeven cookie kan bewaren.
    // De loginpagina gebruikt deze client via CookieAuthenticationStateProvider; ze roept SecureApi dus niet rechtstreeks aan.
    var appBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    builder.Services.AddHttpClient("SecureApi",
            client => client.BaseAddress = appBaseAddress)
                        .AddHttpMessageHandler<CookieHandler>();

    // vanaf nu is "SecureApi" een soort opgeslagen recept voor een HTTP-client

    // als iemand een dagplanningclient vraagt, maak dan een DayPlanningApiService
    // en geef hem een HTTP-client met het "SecureApi"-recept
    builder.Services.AddScoped(sp =>
        new DayPlanningApiService(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("SecureApi")));

    builder.Services.AddScoped<IDayPlanningClient>(sp =>
        sp.GetRequiredService<DayPlanningApiService>());

    // als iemand IProductService vraagt, maak dan een ProductService
    // en geef hem een eigen HTTP-client met dit backendadres
    builder.Services.AddHttpClient<IProductService, ProductService>(client =>
    {
        client.BaseAddress = appBaseAddress;
    });

    await builder.Build().RunAsync();
}           
catch (Exception ex)
{
    Log.Fatal(ex, "An exception occurred while creating the WASM host");
    throw;
}
