using Planning.Shared.Identity;
using Planning.Shared.Identity.Roles;
using Microsoft.AspNetCore.Identity;

namespace Planning.Server.Endpoints.Identity.Roles;

/// <summary>
/// Create a new role.
/// See https://fast-endpoints.com/
/// </summary>
/// <param name="roleManager"></param>
public class Create(RoleManager<IdentityRole> roleManager) : Endpoint<RoleRequest.Create, Result<RoleResponse.Created>>
{
    public override void Configure()
    {
        Post("/api/identity/roles");
        Description(b => b.ClearDefaultProduces(200)
            .Produces<RoleResponse.Created>(201));
        Summary(s => s.ResponseHeaders.Add(new ResponseHeader(201, "Location")
        {
            Description = "URI identifying the created role."
        }));
        Roles(AppRoles.Administrator);
    }

    public override async Task<Result<RoleResponse.Created>> ExecuteAsync(RoleRequest.Create req, CancellationToken ctx)
    {
        if(await roleManager.RoleExistsAsync(req.Name))
            return Result.Conflict($"Role with name '{req.Name}' already exists.");
        
        IdentityRole role = new()
        {
            Name = req.Name,
            NormalizedName = req.Name.ToUpper()
        };

        var result = await roleManager.CreateAsync(role);
        
        if(!result.Succeeded)
            return Result.Error(result.Errors.First().Description);
        
        return Result.Created(new RoleResponse.Created
        {
            Id = role.Id,
            Name = role.Name!
        }, $"/api/identity/roles/{Uri.EscapeDataString(role.Id)}");
    }
}
