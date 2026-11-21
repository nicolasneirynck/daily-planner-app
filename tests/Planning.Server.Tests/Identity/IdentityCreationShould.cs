using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Planning.Server.Tests.DayPlanning;
using Planning.Shared.Identity.Accounts;
using Planning.Shared.Identity.Roles;

namespace Planning.Server.Tests.Identity;

[Collection("IdentityServer")]
public class IdentityCreationShould(IdentityApp app)
{
    [Fact]
    public async Task ReturnCreatedAccountWithLocation()
    {
        // Arrange
        using var client = app.CreateClient();
        var email = $"user-{Guid.NewGuid()}@example.test";

        // Act
        using var response = await client.PostAsJsonAsync(
            "/api/identity/accounts/register",
            new AccountRequest.Register
            {
                Email = email,
                Password = "Password123!",
                ConfirmPassword = "Password123!"
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Content.Headers.ContentType?.MediaType
            .ShouldBe("application/json");
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetString();
        id.ShouldNotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("email").GetString().ShouldBe(email);
        response.Headers.Location?.ToString()
            .ShouldBe($"/api/identity/accounts/{id}");
    }

    [Fact]
    public async Task ReturnCreatedRoleWithLocation()
    {
        // Arrange
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.RoleHeader, "Administrator");
        var name = $"Role-{Guid.NewGuid():N}";

        // Act
        using var response = await client.PostAsJsonAsync(
            "/api/identity/roles", new RoleRequest.Create { Name = name });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Content.Headers.ContentType?.MediaType
            .ShouldBe("application/json");
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetString();
        id.ShouldNotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("name").GetString().ShouldBe(name);
        response.Headers.Location?.ToString()
            .ShouldBe($"/api/identity/roles/{id}");
    }
}
