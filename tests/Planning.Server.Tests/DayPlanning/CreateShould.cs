using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Tests.DayPlanning;

[Collection("DayPlanningServer")]
public class CreateShould(DayPlanningApp app) : IClassFixture<DayPlanningApp>
{
    [Fact]
    public async Task RejectAnUnauthenticatedUser()
    {
        using var client = app.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/day-planning/activities",
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = "Read a book"
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReturnAValidationErrorWhenNameIsMissing(string? name)
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.PostAsJsonAsync(
            "/api/day-planning/activities",
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = name!
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        body.RootElement.GetProperty("status")
            .GetInt32().ShouldBe(400);

        var nameError = body.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Single(error =>
                error.GetProperty("name").GetString() == "name");

        nameError.GetProperty("reason")
            .GetString().ShouldBe("Activity name is required.");
    }

    [Fact]
    public async Task PassTheSelectedDateTimeBlockAndNameToTheService()
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.PostAsJsonAsync(
            "/api/day-planning/activities",
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = "Read a book"
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        app.DayPlanningService.CreatedActivity.ShouldNotBeNull();
        app.DayPlanningService.CreatedActivity.Date
            .ShouldBe(new DateOnly(2026, 11, 14));
        app.DayPlanningService.CreatedActivity.TimeBlockId.ShouldBe(3);
        app.DayPlanningService.CreatedActivity.Name.ShouldBe("Read a book");
    }
}
