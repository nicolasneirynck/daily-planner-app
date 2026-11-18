using System.Net;

namespace Planning.Server.Tests.DayPlanning;

[Collection("DayPlanningServer")]
public class DeleteShould(DayPlanningApp app) : IClassFixture<DayPlanningApp>
{
    [Fact]
    public async Task PassPlannedActivityIdToTheService()
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.DeleteAsync(
            "/api/day-planning/activities/42");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        app.DayPlanningService.DeletedPlannedActivityId.ShouldBe(42);
    }

    [Fact]
    public async Task RejectAnUnauthenticatedUser()
    {
        using var client = app.CreateClient();

        using var response = await client.DeleteAsync(
            "/api/day-planning/activities/42");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
