using System.Net;
using System.Text;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;
using Shouldly;

namespace Planning.Client.Tests.Http;

public class ApiResponseReaderShould
{
    [Fact]
    public async Task UseHttpStatusWhenProblemDetailsAreUnreadable()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(
                "{invalid json",
                Encoding.UTF8,
                "application/problem+json")
        };

        // Act
        var result = await ApiResponseReader.ReadClientAsync(response);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        result.ErrorMessage.ShouldBe("The request failed with HTTP 503.");
        result.FieldErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task KeepTypedNoContentSuccessWithoutReadingABody()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.NoContent);

        // Act
        var result =
            await ApiResponseReader.ReadClientAsync<DayPlanningResponse>(response);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        result.Value.ShouldBeNull();
    }

    [Fact]
    public async Task ReadGeneralErrorFromProblemDetails()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """
                {
                  "status": 409,
                  "detail": "The request conflicts with the current state."
                }
                """,
                Encoding.UTF8,
                "application/problem+json")
        };

        // Act
        var result = await ApiResponseReader.ReadClientAsync(response);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ErrorMessage.ShouldBe(
            "The request conflicts with the current state.");
        result.FieldErrors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadFieldErrorsForTypedRequest()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """
                {
                  "status": 400,
                  "detail": "The request is invalid.",
                  "errors": [
                    { "name": "name", "reason": "Activity name is required." }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/problem+json")
        };

        // Act
        var result =
            await ApiResponseReader.ReadClientAsync<DayPlanningResponse>(response);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Value.ShouldBeNull();
        result.ErrorMessage.ShouldBe("The request is invalid.");
        result.FieldErrors.Count.ShouldBe(1);
        result.FieldErrors[0].ShouldBe(new ClientFieldError(
            "name", "Activity name is required."));
    }

    [Fact]
    public async Task ReadFieldErrorsFromProblemDetails()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """
                {
                  "status": 400,
                  "detail": "The request is invalid.",
                  "errors": [
                    { "name": "name", "reason": "Activity name is required." }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/problem+json")
        };

        // Act
        var result = await ApiResponseReader.ReadClientAsync(response);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ErrorMessage.ShouldBe("The request is invalid.");
        result.FieldErrors.Count.ShouldBe(1);
        result.FieldErrors[0].ShouldBe(new ClientFieldError(
            "name", "Activity name is required."));
    }

    [Fact]
    public async Task ReadUntypedSuccessBody()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"status":0}""",
                Encoding.UTF8,
                "application/json")
        };

        // Act
        var result = await ApiResponseReader.ReadClientAsync(response);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task KeepNoContentSuccessWithoutReadingABody()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.NoContent);

        // Act
        var result = await ApiResponseReader.ReadClientAsync(response);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ReadTypedValueFromExistingSuccessBody()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "value": {
                    "date": "2026-11-14",
                    "timeBlocks": []
                  }
                }
                """,
                Encoding.UTF8,
                "application/json")
        };

        // Act
        var result =
            await ApiResponseReader.ReadClientAsync<DayPlanningResponse>(response);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Value.ShouldNotBeNull();
        result.Value.Date.ShouldBe(new DateOnly(2026, 11, 14));
        result.Value.TimeBlocks.ShouldBeEmpty();
    }
}
