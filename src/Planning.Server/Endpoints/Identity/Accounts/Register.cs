using Microsoft.AspNetCore.Identity;
using Planning.Persistence;
using Planning.Shared.Identity;
using Planning.Shared.Identity.Accounts;

namespace Planning.Server.Endpoints.Identity.Accounts;

/// <summary>
/// Register a new user,
/// See https://fast-endpoints.com/
/// </summary>
/// <param name="userManager"></param>
/// <param name="userStore"></param>
/// <param name="roleManager"></param>
public class Register(UserManager<IdentityUser> userManager, IUserStore<IdentityUser> userStore, RoleManager<IdentityRole> roleManager) : Endpoint<AccountRequest.Register, Result<AccountResponse.Created>>
{
    public override void Configure()
    {
        Post("/api/identity/accounts/register");
        Description(b => b.ClearDefaultProduces(200)
            .Produces<AccountResponse.Created>(201));
        Summary(s => s.ResponseHeaders.Add(new ResponseHeader(201, "Location")
        {
            Description = "URI identifying the created account."
        }));
        AllowAnonymous(); // Open for all at the moment, but you can restrict it to only admins.
                          // Roles(AppRoles.Administrator);
    }

    public override async Task<Result<AccountResponse.Created>> ExecuteAsync(AccountRequest.Register req, CancellationToken ctx)
    {
        if (!userManager.SupportsUserEmail)
        {
            return Result.CriticalError("Requires a user store with email support.");
        }
        if (!await roleManager.RoleExistsAsync(AppRoles.User))
        {
            var createRoleResult = await roleManager.CreateAsync(new IdentityRole(AppRoles.User));
            if (!createRoleResult.Succeeded && !await roleManager.RoleExistsAsync(AppRoles.User))
                return Result.Error(createRoleResult.Errors.First().Description);
        }

        var emailStore = (IUserEmailStore<IdentityUser>)userStore;
        var user = new IdentityUser();
        await userStore.SetUserNameAsync(user, req.Email, CancellationToken.None);
        await emailStore.SetEmailAsync(user, req.Email, CancellationToken.None);
        var result = await userManager.CreateAsync(user, req.Password!);
        
        if (!result.Succeeded)
        {
            return Result.Error(result.Errors.First().Description);
        }

        var addToRoleResult = await userManager.AddToRoleAsync(user, AppRoles.User);
        if (!addToRoleResult.Succeeded)
        {
            var deleteResult = await userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
                return Result.CriticalError("Could not assign the User role or remove the incomplete account.");
            return Result.Error(addToRoleResult.Errors.First().Description);
        }

        
        // You can send a confirmation email by using a SMTP server or anything in the like. 
        // await SendConfirmationEmailAsync(user, userManager, context, email); or do something that matters

        return Result.Created(new AccountResponse.Created
        {
            Id = user.Id,
            Email = user.Email!
        }, $"/api/identity/accounts/{Uri.EscapeDataString(user.Id)}");
    }
    
}
