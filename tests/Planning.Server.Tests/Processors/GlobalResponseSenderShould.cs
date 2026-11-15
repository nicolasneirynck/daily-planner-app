using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ardalis.Result;
using Planning.Server.Tests.DayPlanning;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Tests.Processors;

[Collection("DayPlanningServer")]
public class GlobalResponseSenderShould
{
    [Fact]
    public async Task ReturnProblemDetailsForAnInvalidServiceResult()
    {
        // Arrange: use a separate app so this fake result cannot affect other tests.
        using var invalidApp = new DayPlanningApp();

        // The valid request will pass endpoint validation; the fake service returns the error.
        invalidApp.DayPlanningService.CreateActivityResult =
            Result.Invalid(new ValidationError
            {
                Identifier = nameof(CreatePlannedActivityRequest.Name),
                ErrorMessage = "Activity name is required."
            });

        using var client = invalidApp.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        // Act: send a valid activity name to reach the service error path.
        using var response = await client.PostAsJsonAsync(
            "/api/day-planning/activities",
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = "Read a book"
            });

        // Assert: the HTTP status and response body use the same validation contract.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetInt32().ShouldBe(400);

        // Find the error by field name so its position in the list does not matter.
        var nameError = body.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Single(error =>
                error.GetProperty("name").GetString() == "name");

        nameError.GetProperty("reason")
            .GetString().ShouldBe("Activity name is required.");
    }
}
