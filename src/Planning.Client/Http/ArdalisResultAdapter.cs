using System.Net;

namespace Planning.Client.Http;

internal static class ArdalisResultAdapter
{
    public static Result<T> ToResult<T>(ApiCallResult<Result<T>> response)
    {
        if (response.IsSuccess)
            return response.StatusCode == HttpStatusCode.NoContent
                ? Result<T>.NoContent()
                : response.Value
                  ?? throw new InvalidOperationException("API response was empty.");

        return Failure(response.StatusCode, response.Problem);
    }

    public static Result ToResult(ApiCallResult<Result> response)
    {
        if (response.IsSuccess)
            return response.StatusCode == HttpStatusCode.NoContent
                ? Result.NoContent()
                : response.Value
                  ?? throw new InvalidOperationException("API response was empty.");

        return Failure(response.StatusCode, response.Problem);
    }

    private static Result Failure(HttpStatusCode status, ApiProblem? problem)
    {
        var detail = string.IsNullOrWhiteSpace(problem?.Detail)
            ? $"The request failed with HTTP {(int)status}."
            : problem.Detail;

        if (status == HttpStatusCode.BadRequest)
        {
            var errors = problem?.Errors?
                .Where(error => !string.IsNullOrWhiteSpace(error.Reason))
                .Select(error => new ValidationError
                {
                    Identifier = string.IsNullOrWhiteSpace(error.Name)
                        ? "$"
                        : error.Name,
                    ErrorMessage = error.Reason!
                })
                .ToList() ?? [];

            if (!errors.Any(error =>
                    error.Identifier == "$" && error.ErrorMessage == detail))
            {
                errors.Add(new ValidationError
                {
                    Identifier = "$",
                    ErrorMessage = detail
                });
            }

            return Result.Invalid(errors);
        }

        return status switch
        {
            HttpStatusCode.Unauthorized => Result.Unauthorized(detail),
            HttpStatusCode.Forbidden => Result.Forbidden(detail),
            HttpStatusCode.NotFound => Result.NotFound(detail),
            HttpStatusCode.Conflict => Result.Conflict(detail),
            HttpStatusCode.InternalServerError => Result.CriticalError(detail),
            HttpStatusCode.ServiceUnavailable => Result.Unavailable(detail),
            _ => Result.Error(detail)
        };
    }
}
