using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ardalis.Result;
using Planning.Server.Tests.DayPlanning;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Tests.Errors;

[Collection("DayPlanningServer")]
public class ApiProblemContractShould(DayPlanningApp app) : IClassFixture<DayPlanningApp>
{
    [Fact]
    public async Task ReturnProblemDetailsForEndpointValidation()
    {
        // Arrange
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        // Act
        using var response = await client.PostAsJsonAsync(
            "/api/day-planning/activities",
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = ""
            });

        // Assert
        var body = await ReadProblemAsync(
            response, HttpStatusCode.BadRequest, "Bad Request");

        var errors = body.GetProperty("errors").EnumerateArray().ToArray(); // EnumerateArray() geeft een enumerator waarmee je de elementen één voor één kunt doorlopen. ToArray() leest ze meteen uit en bewaart ze in een C#-array
        errors.Any(error =>
            error.GetProperty("name").GetString() == "name" &&
            error.GetProperty("reason").GetString() == "Activity name is required.").ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnProblemDetailsForUnauthenticatedRequest()
    {
        // Arrange
        using var client = app.CreateClient();

        // Act
        using var response = await client.GetAsync(
            "/api/day-planning/2026-11-14");

        // Assert
        var body = await ReadProblemAsync(
            response, HttpStatusCode.Unauthorized, "Unauthorized");
        body.GetProperty("detail").GetString()
            .ShouldBe("Authentication is required.");

        body.TryGetProperty("errors", out _).ShouldBeFalse(); // TryGetProperty heeft een out-parameter die de gevonden JSON-waarde kan teruggeven -> met "out _" geef je aan van ik heb die waarde niet nodig; soort discard functie
    }

    [Fact]
    public async Task ReturnProblemDetailsForUnknownApiRoute()
    {
        // Arrange
        using var client = app.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/does-not-exist");

        // Assert
        var body = await ReadProblemAsync(
            response, HttpStatusCode.NotFound, "Not Found");
        body.GetProperty("detail").GetString()
            .ShouldBe("The requested resource was not found.");
        body.TryGetProperty("errors", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ReturnProblemDetailsForForbiddenRequest()
    {
        // Arrange
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        // Act
        using var response = await client.GetAsync("/api/identity/roles");

        // Assert
        var body = await ReadProblemAsync(
            response, HttpStatusCode.Forbidden, "Forbidden");
        body.GetProperty("detail").GetString()
            .ShouldBe("Access is forbidden.");
        body.TryGetProperty("errors", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ReturnProblemDetailsForServiceConflict()
    {
        // Arrange
        using var serviceErrorApp = new DayPlanningApp();
        serviceErrorApp.DayPlanningService.CreateActivityOverride = _ =>
            Result.Conflict("Internal planner state: secret"); // de service meldt een conflict, met een interne tekst die niet voor de gebruiker bedoeld is

        using var client = serviceErrorApp.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        // Act
        using var response = await client.PostAsJsonAsync(
            "/api/day-planning/activities",
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = "Read a book"
            });

        // Assert
        var body = await ReadProblemAsync(
            response, HttpStatusCode.Conflict, "Conflict");
        body.GetProperty("detail").GetString()
            .ShouldBe("The request conflicts with the current state.");
        body.TryGetProperty("errors", out _).ShouldBeFalse();
        body.GetRawText().ShouldNotContain("Internal planner state: secret");
    }

    [Fact]
    public async Task ReturnSafeProblemDetailsForUnexpectedException()
    {
        // Arrange
        using var failingApp = new DayPlanningApp();
        failingApp.DayPlanningService.CreateActivityOverride = _ =>
            throw new InvalidOperationException("Secret database connection details");

        using var client = failingApp.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        // Act
        using var response = await client.PostAsJsonAsync(
            "/api/day-planning/activities",
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = "Read a book"
            });

        // Assert
        var body = await ReadProblemAsync(
            response, HttpStatusCode.InternalServerError, "Internal Server Error");
        body.GetProperty("detail").GetString()
            .ShouldBe("An unexpected error occurred.");
        body.GetRawText().ShouldNotContain("Secret database connection details");
    }

    private static async Task<JsonElement> ReadProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle)
    {
        response.StatusCode.ShouldBe(expectedStatus);
        response.Content.Headers.ContentType?.MediaType
            .ShouldBe("application/problem+json");

        // Leest de responsebody als JSON; using var ruimt JsonDocument aan het einde van de methode op.
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var body = document.RootElement.Clone();

        body.GetProperty("type").GetString().ShouldBe("about:blank");
        body.GetProperty("title").GetString().ShouldBe(expectedTitle);
        body.GetProperty("status").GetInt32().ShouldBe((int)expectedStatus);
        body.GetProperty("detail").GetString().ShouldNotBeNullOrWhiteSpace();

        var instance = body.GetProperty("instance").GetString();
        instance.ShouldNotBeNullOrWhiteSpace();
        instance!.ShouldStartWith("urn:uuid:");
        Guid.TryParse(instance["urn:uuid:".Length..], out _).ShouldBeTrue();

        body.TryGetProperty("isSuccess", out _).ShouldBeFalse();
        return body;
    }
}
