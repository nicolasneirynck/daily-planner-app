using System.Net;
using System.Text.Json;

namespace Planning.Server.Tests.DayPlanning;

public class GetShould(DayPlanningApp app) : IClassFixture<DayPlanningApp>
{
    [Fact]
    public async Task ReturnTheRequestedDayForAnAuthenticatedUser()
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.GetAsync(
            "/api/day-planning/2026-11-14");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        app.DayPlanningService.RequestedDate
            .ShouldBe(new DateOnly(2026, 11, 14));

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("value")
            .GetProperty("date").GetString()
            .ShouldBe("2026-11-14");

        var block = body.RootElement.GetProperty("value")
            .GetProperty("timeBlocks")[0];
        block.GetProperty("startTime").GetString().ShouldBe("06:00:00");
        block.GetProperty("endTime").GetString().ShouldBe("09:00:00");
    }

    [Fact]
    public async Task RejectAnInvalidDate()
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.GetAsync(
            "/api/day-planning/not-a-date");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RejectAnUnauthenticatedUser()
    {
        using var client = app.CreateClient();

        using var response = await client.GetAsync(
            "/api/day-planning/2026-11-14");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
