using System.Net;
using System.Text;
using NSubstitute;
using Planning.Client.Identity;
using Shouldly;

namespace Planning.Client.Tests.Identity;

public class CookieAuthenticationStateProviderShould
{
    [Fact]
    public async Task AcceptNoContentLogoutSuccess()
    {
        // Arrange
        using var client = new HttpClient(new StubHandler(request =>
            request.RequestUri!.AbsolutePath switch
            {
                "/api/identity/accounts/logout" =>
                    new HttpResponseMessage(HttpStatusCode.NoContent),
                "/api/identity/accounts/info" =>
                    new HttpResponseMessage(HttpStatusCode.Unauthorized),
                _ => throw new InvalidOperationException(
                    "Unexpected identity request.")
            }))
        {
            BaseAddress = new Uri("https://example.test")
        };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SecureApi").Returns(client);
        var provider = new CookieAuthenticationStateProvider(factory);

        // Act
        var result = await provider.LogoutAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ReturnGeneralLogoutError()
    {
        // Arrange
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """
                    {
                      "status": 409,
                      "detail": "Could not log out."
                    }
                    """,
                    Encoding.UTF8,
                    "application/problem+json")
            }))
        {
            BaseAddress = new Uri("https://example.test")
        };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SecureApi").Returns(client);
        var provider = new CookieAuthenticationStateProvider(factory);

        // Act
        var result = await provider.LogoutAsync();

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ErrorMessage.ShouldBe("Could not log out.");
    }

    [Fact]
    public async Task ReturnInvalidCredentialsMessageForUnauthorizedLogin()
    {
        // Arrange
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    """
                    {
                      "status": 401,
                      "detail": "Authentication is required."
                    }
                    """,
                    Encoding.UTF8,
                    "application/problem+json")
            }))
        {
            BaseAddress = new Uri("https://example.test")
        };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SecureApi").Returns(client);
        var provider = new CookieAuthenticationStateProvider(factory);

        // Act
        var result = await provider.LoginAsync(
            "alex@example.test", "wrong-password");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        result.ErrorMessage.ShouldBe("Invalid email and/or password.");
    }

    [Fact]
    public async Task ReturnRegistrationFieldError()
    {
        // Arrange
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """
                    {
                      "status": 400,
                      "detail": "The request is invalid.",
                      "errors": [
                        { "name": "email", "reason": "Email is invalid." }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/problem+json")
            }))
        {
            BaseAddress = new Uri("https://example.test")
        };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SecureApi").Returns(client);
        var provider = new CookieAuthenticationStateProvider(factory);

        // Act
        var result = await provider.RegisterAsync(
            "invalid", "Password123!", "Password123!");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.FieldErrors.Any(error =>
            error.Name == "email" &&
            error.Message == "Email is invalid.").ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAuthenticatedStateFromExistingInfoBody()
    {
        // Arrange
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "value": {
                        "email": "alex@example.test",
                        "isEmailConfirmed": true,
                        "claims": {},
                        "roles": ["User"]
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }))
        {
            BaseAddress = new Uri("https://example.test")
        };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SecureApi").Returns(client);
        var provider = new CookieAuthenticationStateProvider(factory);

        // Act
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        state.User.Identity!.IsAuthenticated.ShouldBeTrue();
        state.User.Identity.Name.ShouldBe("alex@example.test");
        state.User.IsInRole("User").ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnAnonymousStateForExpectedUnauthorizedInfoResponse()
    {
        // Arrange
        using var client = new HttpClient(new StubHandler(request =>
        {
            request.RequestUri!.AbsolutePath
                .ShouldBe("/api/identity/accounts/info");

            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    """
                    {
                      "status": 401,
                      "detail": "Authentication is required."
                    }
                    """,
                    Encoding.UTF8,
                    "application/problem+json")
            };
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SecureApi").Returns(client);
        var provider = new CookieAuthenticationStateProvider(factory);

        // Act
        var state = await provider.GetAuthenticationStateAsync();

        // Assert
        state.User.Identity!.IsAuthenticated.ShouldBeFalse();
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
