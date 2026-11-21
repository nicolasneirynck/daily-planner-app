using System.Net;
using System.Net.Http.Json;

namespace Planning.Server.Tests.DayPlanning;

// [Collection("DayPlanningServer")] zet SetCompletionShould, CreateShould en GetShould in dezelfde xUnit-testcollectie.
// xUnit voert die testklassen daardoor niet tegelijk uit.
[Collection("DayPlanningServer")]
/* DayPlanningApp is de testversie van de server.
    De test kan er met app.CreateClient() echte HTTP-verzoeken naartoe sturen, zonder de applicatie apart op te starten. 
    In die testserver wordt de echte dagplanningservice vervangen door RecordingDayPlanningService, 
    zodat je kunt controleren welke waarden het endpoint doorgeeft.
   
   IClassFixture<DayPlanningApp> vertelt xUnit om één DayPlanningApp te maken en te delen tussen de tests in deze klasse. Via SetCompletionShould(DayPlanningApp app) krijgt de testklasse die instantie.*/
public class SetCompletionShould(DayPlanningApp app) : IClassFixture<DayPlanningApp> 

{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PassActivityIdAndCompletionStatusToTheService(
        bool isCompleted)
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.PutAsJsonAsync(
            "/api/day-planning/activities/42/completion",
            new { isCompleted });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        response.Content.Headers.ContentType.ShouldBeNull();
        app.DayPlanningService.UpdatedActivityId.ShouldBe(42);
        app.DayPlanningService.UpdatedIsCompleted.ShouldBe(isCompleted);
    }

    [Fact]
    public async Task RejectAnUnauthenticatedUser()
    {
        using var client = app.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/api/day-planning/activities/42/completion",
            new { isCompleted = true });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
