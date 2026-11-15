using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Planning.Shared.DayPlanning;

namespace Planning.Client.DayPlanning;

public class DayPlanningApiService(HttpClient httpClient) : IDayPlanningService
{
    public async Task<Result> CreateActivityAsync(
        CreatePlannedActivityRequest request, CancellationToken ctx = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "/api/day-planning/activities", request, ctx);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            using var problem = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ctx));

            var errors = problem.RootElement.GetProperty("errors")
                .EnumerateArray()
                .Select(error =>
                {
                    var name = error.GetProperty("name").GetString();

                    return new ValidationError
                    {
                        Identifier = name == "name"
                            ? nameof(CreatePlannedActivityRequest.Name)
                            : name ?? string.Empty,
                        ErrorMessage =
                            error.GetProperty("reason").GetString() ?? string.Empty
                    };
                })
                .ToArray();

            return Result.Invalid(errors);
        }

        return await response.Content.ReadFromJsonAsync<Result>(
            cancellationToken: ctx)
            ?? throw new InvalidOperationException(
                "Create activity response was empty.");
    }

    public async Task<Result<DayPlanningResponse>> GetDayAsync(
        DateOnly date, CancellationToken ctx = default)
    {
        return await httpClient.GetFromJsonAsync<Result<DayPlanningResponse>>(
            $"/api/day-planning/{date:yyyy-MM-dd}", ctx)
            ?? throw new InvalidOperationException("Day planning response was empty.");
    }
}
