using System.Net;
using System.Text.Json;
using Ardalis.Result;

namespace Planning.Server.Tests.DayPlanning;

[Collection("DayPlanningServer")]
public class GetActivityShould(DayPlanningApp app) : IClassFixture<DayPlanningApp>
{
    [Fact]
    public async Task ReturnActivityDetails()
    {
        app.DayPlanningService.GetActivityOverride = null;

        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.GetAsync(
            "/api/day-planning/activities/42");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(
            "application/json");
        app.DayPlanningService.RequestedActivityId.ShouldBe(42);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;
        root.GetProperty("id").GetInt32().ShouldBe(42);
        root.GetProperty("name").GetString().ShouldBe("Prepare presentation");
        root.GetProperty("description").GetString().ShouldBe("Bring the slides");
        root.GetProperty("date").GetString().ShouldBe("2026-11-24");
        root.GetProperty("timeBlockId").GetInt32().ShouldBe(3);
        root.GetProperty("timeBlockName").GetString().ShouldBe("Afternoon");
        root.GetProperty("startTime").GetString().ShouldBe("13:00:00");
        root.GetProperty("endTime").GetString().ShouldBe("16:00:00");
        root.GetProperty("isImportant").GetBoolean().ShouldBeTrue();
        root.GetProperty("isCompleted").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task ReturnNotFoundWhenTheServiceCannotFindTheActivity()
    {
        app.DayPlanningService.GetActivityOverride = _ =>
            Result<Planning.Shared.DayPlanning.PlannedActivityDetailsDto>
                .NotFound();

        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.GetAsync(
            "/api/day-planning/activities/42");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejectAnUnauthenticatedUser()
    {
        using var client = app.CreateClient();

        using var response = await client.GetAsync(
            "/api/day-planning/activities/42");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
