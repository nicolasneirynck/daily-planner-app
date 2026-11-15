using System.Net;
using System.Text;
using Ardalis.Result;
using Planning.Client.DayPlanning;
using Planning.Shared.DayPlanning;
using Shouldly;

namespace Planning.Client.Tests.DayPlanning;

public class DayPlanningApiServiceShould
{
    [Fact]
    public async Task ReturnNameValidationErrorFromBadRequest()
    {
        // Arrange: simulate the Problem Details response from the API.
        using var client = new HttpClient(new BadRequestHandler())
        {
            BaseAddress = new Uri("https://example.test")
        };
        var service = new DayPlanningApiService(client);

        // Act: send an activity that the API would reject.
        var result = await service.CreateActivityAsync(
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = "   "
            });

        // Assert: the UI receives a validation result for its Name property.
        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ValidationErrors.Single();
        error.Identifier.ShouldBe(nameof(CreatePlannedActivityRequest.Name));
        error.ErrorMessage.ShouldBe("Activity name is required.");
    }

    private sealed class BadRequestHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const string json = """
                {
                  "status": 400,
                  "errors": [
                    {
                      "name": "name",
                      "reason": "Activity name is required."
                    }
                  ]
                }
                """;

            return Task.FromResult(new HttpResponseMessage(
                HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    json, Encoding.UTF8, "application/problem+json")
            });
        }
    }
}
