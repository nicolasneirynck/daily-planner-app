using Microsoft.AspNetCore.Identity;

namespace Planning.Server.Endpoints.Identity.Accounts;

/// <summary>
/// Logout Endpoint.
/// See https://fast-endpoints.com/
/// </summary>
/// <param name="signInManager"></param>
public class Logout(SignInManager<IdentityUser> signInManager) : EndpointWithoutRequest<Result>
{
    public override void Configure()
    {
        Post("/api/identity/accounts/logout");
        Description(b => b.ClearDefaultProduces(200, 204).Produces(204));
    }

    public override async Task<Result> ExecuteAsync(CancellationToken ct)
    {
        await signInManager.SignOutAsync();
        return Result.NoContent();
    }
}
