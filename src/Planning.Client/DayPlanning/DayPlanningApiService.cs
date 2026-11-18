using System.Net.Http.Json;
using Planning.Shared.DayPlanning;

namespace Planning.Client.DayPlanning;

public class DayPlanningApiService(HttpClient httpClient) : IDayPlanningService
{
    public async Task<Result<DayPlanningResponse>> GetDayAsync(
        DateOnly date, CancellationToken ctx = default)
    {
        return await httpClient.GetFromJsonAsync<Result<DayPlanningResponse>>(
            $"/api/day-planning/{date:yyyy-MM-dd}", ctx)
            ?? throw new InvalidOperationException("Day planning response was empty.");
    }

    public async Task<Result> CreateActivityAsync(
        CreatePlannedActivityRequest request, CancellationToken ctx = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "/api/day-planning/activities", request, ctx);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Result>(
            cancellationToken: ctx)
            ?? throw new InvalidOperationException(
                "Create activity response was empty.");
    }

    public async Task<Result> SetActivityCompletionAsync(
        int activityId, bool isCompleted, CancellationToken ctx = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"/api/day-planning/activities/{activityId}/completion",
            new SetActivityCompletionRequest { IsCompleted = isCompleted },
            ctx);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Result>(
            cancellationToken: ctx)
            ?? throw new InvalidOperationException(
                "Set activity completion response was empty.");
    }

    public async Task<Result> DeletePlannedActivityAsync(
        int plannedActivityId, CancellationToken ctx = default)
    {
        using var response = await httpClient.DeleteAsync(
            $"/api/day-planning/activities/{plannedActivityId}", ctx);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Result>(
            cancellationToken: ctx)
            ?? throw new InvalidOperationException(
                "Delete planned activity response was empty.");
    }
}
