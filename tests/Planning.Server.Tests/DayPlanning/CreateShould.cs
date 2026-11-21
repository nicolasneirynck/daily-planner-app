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
        (await response.Content.ReadAsStringAsync())
            .ShouldContain("Activity name is required.");
    }

    [Fact]
    public async Task ReturnAValidationErrorWhenDescriptionIsTooLong()
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.PostAsJsonAsync(
            "/api/day-planning/activities",
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = "Read a book",
                Description = new string('a', 501)
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Description must be 500 characters or fewer.");
    }

    [Fact]
    public async Task PassTheSelectedDateTimeBlockNameAndDescriptionToTheService()
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
                Name = "Read a book",
                Description = "Bring the library card"
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location?.ToString()
            .ShouldBe("/api/day-planning/activities/42");
        response.Content.Headers.ContentType?.MediaType
            .ShouldBe("application/json");

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        root.GetProperty("id").GetInt32().ShouldBe(42);
        root.GetProperty("name").GetString().ShouldBe("Read a book");
        root.GetProperty("description").GetString()
            .ShouldBe("Bring the library card");
        app.DayPlanningService.CreatedActivity.ShouldNotBeNull();
        app.DayPlanningService.CreatedActivity.Date
            .ShouldBe(new DateOnly(2026, 11, 14));
        app.DayPlanningService.CreatedActivity.TimeBlockId.ShouldBe(3);
        app.DayPlanningService.CreatedActivity.Name.ShouldBe("Read a book");
        app.DayPlanningService.CreatedActivity.Description
            .ShouldBe("Bring the library card");
    }
}
