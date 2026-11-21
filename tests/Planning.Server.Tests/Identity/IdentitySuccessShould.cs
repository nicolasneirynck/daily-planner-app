using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Planning.Server.Tests.DayPlanning;
using Planning.Shared.Identity.Accounts;
using Planning.Shared.Identity.Roles;

namespace Planning.Server.Tests.Identity;

[Collection("IdentityServer")]
public class IdentitySuccessShould(IdentityApp app)
{
    [Fact]
    public async Task ReturnAccountInfoAsJson()
    {
        // Arrange
        using var client = app.CreateClient();
        var (id, email) = await RegisterUserAsync(client);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, id);

        // Act
        using var response = await client.GetAsync(
            "/api/identity/accounts/info");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .ShouldBe("application/json");
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("email").GetString().ShouldBe(email);
        body.RootElement.GetProperty("roles").ValueKind
            .ShouldBe(JsonValueKind.Array);
    }

    [Fact]
    public async Task ReturnRoleListAsJson()
    {
        // Arrange
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.RoleHeader, "Administrator");
        var name = $"Listed-{Guid.NewGuid():N}";
        using var created = await client.PostAsJsonAsync(
            "/api/identity/roles", new RoleRequest.Create { Name = name });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        using var response = await client.GetAsync("/api/identity/roles");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .ShouldBe("application/json");
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        body.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        body.RootElement.EnumerateArray().Any(role =>
            !string.IsNullOrWhiteSpace(
                role.GetProperty("key").GetString()) &&
            role.GetProperty("value").GetString() == name)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnEmptyBodyAndCookieOnLogin()
    {
        // Arrange
        using var client = app.CreateClient();
        var (_, email) = await RegisterUserAsync(client);

        // Act
        using var response = await client.PostAsJsonAsync(
            "/api/identity/accounts/login",
            new AccountRequest.Login
            {
                Email = email,
                Password = "Password123!"
            });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        response.Content.Headers.ContentType.ShouldBeNull();
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            .ShouldBeTrue();
        cookies!.ShouldContain(cookie =>
            cookie.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReturnEmptyBodyAndClearCookieOnLogout()
    {
        // Arrange
        using var client = app.CreateClient();
        var (id, _) = await RegisterUserAsync(client);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, id);

        // Act
        using var response = await client.PostAsync(
            "/api/identity/accounts/logout", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        response.Content.Headers.ContentType.ShouldBeNull();
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            .ShouldBeTrue();
        cookies!.ShouldContain(cookie =>
            cookie.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
    }

    private static async Task<(string Id, string Email)> RegisterUserAsync(
        HttpClient client)
    {
        var email = $"user-{Guid.NewGuid()}@example.test";
        using var response = await client.PostAsJsonAsync(
            "/api/identity/accounts/register",
            new AccountRequest.Register
            {
                Email = email,
                Password = "Password123!",
                ConfirmPassword = "Password123!"
            });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        return (body.RootElement.GetProperty("id").GetString()!, email);
    }
}
