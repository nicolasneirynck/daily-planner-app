using System.Net;
using System.Net.Http.Json;
using System.Text;
using Planning.Client.DayPlanning;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;

namespace Planning.Client.Tests.DayPlanning;

public class DayPlanningApiClientShould
{
    [Fact]
    public async Task ReadDayThroughClientInterface()
    {
        // Arrange
        using var client = new HttpClient(new StubHandler(request =>
        {
            request.RequestUri!.AbsolutePath
                .ShouldBe("/api/day-planning/2026-11-14");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "date": "2026-11-14",
                      "timeBlocks": []
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        IDayPlanningClient service = new DayPlanningApiClient(client);

        // Act
        var result = await service.GetDayAsync(new DateOnly(2026, 11, 14));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Value.ShouldNotBeNull();
        result.Value.Date.ShouldBe(new DateOnly(2026, 11, 14));
    }

    [Fact]
    public async Task ReadActivityThroughClientInterface()
    {
        // Arrange
        using var client = new HttpClient(new StubHandler(request =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            request.RequestUri!.AbsolutePath
                .ShouldBe("/api/day-planning/activities/42");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": 42,
                      "name": "Morning walk",
                      "description": "Bring a jacket",
                      "date": "2026-11-24",
                      "timeBlockId": 1,
                      "timeBlockName": "Ochtend",
                      "startTime": "06:00:00",
                      "endTime": "09:00:00",
                      "isImportant": true,
                      "isCompleted": false
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        IDayPlanningClient service = new DayPlanningApiClient(client);

        // Act
        var result = await service.GetActivityAsync(42);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldBe(42);
        result.Value.Name.ShouldBe("Morning walk");
        result.Value.Date.ShouldBe(new DateOnly(2026, 11, 24));
        result.Value.TimeBlockName.ShouldBe("Ochtend");
        result.Value.IsImportant.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateActivityThroughClientInterface()
    {
        // Arrange
        HttpRequestMessage? sentRequest = null;
        using var client = new HttpClient(new StubHandler(request =>
        {
            sentRequest = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        IDayPlanningClient service = new DayPlanningApiClient(client);
        var update = new UpdatePlannedActivityRequest
        {
            Date = new DateOnly(2026, 11, 24),
            TimeBlockId = 2,
            Name = "Evening walk",
            Description = "Bring a jacket",
            IsImportant = true
        };

        // Act
        var result = await service.UpdateActivityAsync(42, update);

        // Assert
        sentRequest.ShouldNotBeNull();
        sentRequest.Method.ShouldBe(HttpMethod.Put);
        sentRequest.RequestUri!.AbsolutePath
            .ShouldBe("/api/day-planning/activities/42");

        var body = await sentRequest.Content!
            .ReadFromJsonAsync<UpdatePlannedActivityRequest>();
        body.ShouldNotBeNull();
        body.Date.ShouldBe(update.Date);
        body.TimeBlockId.ShouldBe(update.TimeBlockId);
        body.Name.ShouldBe(update.Name);
        body.Description.ShouldBe(update.Description);
        body.IsImportant.ShouldBe(update.IsImportant);

        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MapProblemValidationErrorsToFieldErrors()
    {
        // Arrange
        const string problem = """
            {
              "type": "about:blank",
              "title": "Bad Request",
              "status": 400,
              "detail": "The request is invalid.",
              "instance": "urn:uuid:00000000-0000-0000-0000-000000000001",
              "errors": [
                { "name": "name", "reason": "Activity name is required." }
              ]
            }
            """;

        using var client = new HttpClient(new StubHandler(_ => // using var client ruimt de HttpClient na afloop op. -> betekent dat C# aan het einde van de huidige methode automatisch client.Dispose() -> “Opruimen” betekent hier dat de client zijn gebruikte middelen vrijgeeft;
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    problem, Encoding.UTF8, "application/problem+json") // zet de tekst in problem in de responsebody, met UTF-8 en het Problem Details-contenttype.
            }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        /*
         * Dit is een object initializer: tussen de accolades { ... } stel je direct na het maken van de HttpClient een eigenschap in.
           using var client = new HttpClient(handler)
           {
               BaseAddress = new Uri("https://example.test")
           };

           BaseAddress is het basisadres voor relatieve verzoeken. Als de geteste code bijvoorbeeld /api/day-planning opvraagt,
           kan HttpClient daar https://example.test/api/day-planning van maken.
         */
        IDayPlanningClient service = new DayPlanningApiClient(client);

        // Act
        var result = await service.CreateActivityAsync(
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = "Read a book"
            });

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ErrorMessage.ShouldBe("The request is invalid.");
        result.FieldErrors.Count.ShouldBe(1);
        result.FieldErrors[0].ShouldBe(new ClientFieldError(
            "name", "Activity name is required."));
    }

    [Fact]
    public async Task MapProblemDetailToGeneralError()
    {
        // Arrange
        const string problem = """
            {
              "type": "about:blank",
              "title": "Conflict",
              "status": 409,
              "detail": "The request conflicts with the current state.",
              "instance": "urn:uuid:00000000-0000-0000-0000-000000000002"
            }
            """;

        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    problem, Encoding.UTF8, "application/problem+json")
            }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        IDayPlanningClient service = new DayPlanningApiClient(client);

        // Act
        var result = await service.CreateActivityAsync(
            new CreatePlannedActivityRequest
            {
                Date = new DateOnly(2026, 11, 14),
                TimeBlockId = 3,
                Name = "Read a book"
            });

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ErrorMessage.ShouldBe(
            "The request conflicts with the current state.");
        result.FieldErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task KeepNoContentSuccessWithoutReadingABody()
    {
        // Arrange
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NoContent)))
        {
            BaseAddress = new Uri("https://example.test")
        };
        IDayPlanningClient service = new DayPlanningApiClient(client);

        // Act
        var result = await service.DeletePlannedActivityAsync(42);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /* nep-HTTP-handler voor tests. Hij laat een test bepalen welk antwoord een HttpClient krijgt, zonder een echte server aan te roepen */
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(respond(request)); // respond is een functie die een HTTP-verzoek ontvangt en een HTTP-response teruggeeft
            /* De handler geeft het verzoek aan respond(request) en verpakt de gemaakte response
             met Task.FromResult in het asynchrone type dat HttpClient verwacht.

             Bijvoorbeeld:
               var handler = new StubHandler(_ =>
                   new HttpResponseMessage(HttpStatusCode.BadRequest));
               var client = new HttpClient(handler);
               Elk verzoek via deze client krijgt dan een 400 Bad Request terug.*/
    }
}
