using System.Net.Http.Json;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;

namespace Planning.Client.DayPlanning;

public class DayPlanningApiService(HttpClient httpClient) : IDayPlanningService
{
    public async Task<Result<DayPlanningResponse>> GetDayAsync(
        DateOnly date, CancellationToken ctx = default)
    {
        using var response = await httpClient.GetAsync(
            $"/api/day-planning/{date:yyyy-MM-dd}", ctx);

        var apiResult =
            await ApiResponseReader.ReadAsync<Result<DayPlanningResponse>>(
                response, ctx);

        return ArdalisResultAdapter.ToResult(apiResult);
    }

    public async Task<Result> CreateActivityAsync(
        CreatePlannedActivityRequest request, CancellationToken ctx = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "/api/day-planning/activities", request, ctx);

        return ArdalisResultAdapter.ToResult(
            await ApiResponseReader.ReadAsync<Result>(response, ctx));
    }

    public async Task<Result> SetActivityCompletionAsync(
        int activityId, bool isCompleted, CancellationToken ctx = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"/api/day-planning/activities/{activityId}/completion",
            new SetActivityCompletionRequest { IsCompleted = isCompleted },
            ctx);

        return ArdalisResultAdapter.ToResult(
            await ApiResponseReader.ReadAsync<Result>(response, ctx));
    }

    public async Task<Result> DeletePlannedActivityAsync(
        int plannedActivityId, CancellationToken ctx = default)
    {
        using var response = await httpClient.DeleteAsync(
            $"/api/day-planning/activities/{plannedActivityId}", ctx);

        return ArdalisResultAdapter.ToResult(
            await ApiResponseReader.ReadAsync<Result>(response, ctx));
    }
}
