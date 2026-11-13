using System.Security.Claims;
using Planning.Services.Identity;

namespace Planning.Services.Tests.Fakers;

public class FakeSessionContextProvider : ISessionContextProvider
{
    public FakeSessionContextProvider(ClaimsPrincipal user) => User = user;
    public ClaimsPrincipal? User { get; }
}