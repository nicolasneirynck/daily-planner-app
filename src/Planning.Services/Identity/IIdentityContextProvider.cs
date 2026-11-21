using System.Security.Claims;

namespace Planning.Services.Identity;

/// <summary>
/// Interace for the session context provider, can be easily mocked in test case scenarios.
/// So if you're testing a Service that uses the session provider, you can mock the provider.
/// For example in a service that needs the current user.
/// </summary>
public interface ISessionContextProvider
{
    ClaimsPrincipal? User { get; } 
}