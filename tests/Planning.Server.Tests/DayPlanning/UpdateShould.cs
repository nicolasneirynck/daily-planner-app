using System.Net;
using System.Net.Http.Json;
using Planning.Domain.PlannedActivities;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Tests.DayPlanning;

[Collection("DayPlanningServer")]
public class UpdateShould(DayPlanningApp app) : IClassFixture<DayPlanningApp>
{
    [Fact]
    public async Task PassActivityIdAndFieldsToTheService()
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.PutAsJsonAsync(
            "/api/day-planning/activities/42",
            new UpdatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 15),
                TimeBlockId = 3,
                Name = "Read two chapters",
                Description = "Bring notes",
                IsImportant = true
            });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        response.Content.Headers.ContentType.ShouldBeNull();

        app.DayPlanningService.EditedActivityId.ShouldBe(42);
        var request = app.DayPlanningService.EditedActivityRequest;
        request.ShouldNotBeNull();
        request.Date.ShouldBe(new DateOnly(2026, 11, 15));
        request.TimeBlockId.ShouldBe(3);
        request.Name.ShouldBe("Read two chapters");
        request.Description.ShouldBe("Bring notes");
        request.IsImportant.ShouldBeTrue();
    }

    [Fact]
    public async Task RejectAnUnauthenticatedUser()
    {
        using var client = app.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/api/day-planning/activities/42",
            new UpdatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 15),
                TimeBlockId = 3,
                Name = "Read two chapters",
                IsImportant = false
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, true)]
    public async Task ReturnAValidationErrorForAnInvalidName(
        string? name, bool isTooLong)
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        var requestedName = isTooLong
            ? new string('a', PlannedActivityRules.MaxNameLength + 1)
            : name;

        using var response = await client.PutAsJsonAsync(
            "/api/day-planning/activities/42",
            new UpdatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 15),
                TimeBlockId = 3,
                Name = requestedName!,
                IsImportant = false
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain(
            isTooLong
                ? PlannedActivityRules.NameTooLongMessage
                : PlannedActivityRules.NameRequiredMessage);
    }

    [Fact]
    public async Task ReturnAValidationErrorWhenDescriptionIsTooLong()
    {
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());

        using var response = await client.PutAsJsonAsync(
            "/api/day-planning/activities/42",
            new UpdatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 15),
                TimeBlockId = 3,
                Name = "Read two chapters",
                Description = new string(
                    'a', PlannedActivityRules.MaxDescriptionLength + 1),
                IsImportant = false
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain(
            PlannedActivityRules.DescriptionTooLongMessage);
    }
}
