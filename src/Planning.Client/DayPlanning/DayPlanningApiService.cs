using System.Net.Http.Json;
using Planning.Client.Http;
using Planning.Shared.DayPlanning;

namespace Planning.Client.DayPlanning;

public class DayPlanningApiService(HttpClient httpClient) : IDayPlanningClient
{
    async Task<ClientResult<DayPlanningResponse>> IDayPlanningClient.GetDayAsync(
        DateOnly date, CancellationToken ctx)
    {
        using var response = await httpClient.GetAsync(
            $"/api/day-planning/{date:yyyy-MM-dd}", ctx);

        return await ApiResponseReader.ReadClientAsync<DayPlanningResponse>(
            response, ctx);
    }

    async Task<ClientResult> IDayPlanningClient.CreateActivityAsync(
        CreatePlannedActivityRequest request, CancellationToken ctx)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "/api/day-planning/activities", request, ctx);

        return await ApiResponseReader.ReadClientAsync(response, ctx);
    }

    async Task<ClientResult> IDayPlanningClient.SetActivityCompletionAsync(
        int activityId, bool isCompleted, CancellationToken ctx)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"/api/day-planning/activities/{activityId}/completion",
            new SetActivityCompletionRequest { IsCompleted = isCompleted },
            ctx);

        return await ApiResponseReader.ReadClientAsync(response, ctx);
    }

    async Task<ClientResult> IDayPlanningClient.DeletePlannedActivityAsync(
        int plannedActivityId, CancellationToken ctx)
    {
        using var response = await httpClient.DeleteAsync(
            $"/api/day-planning/activities/{plannedActivityId}", ctx);

        return await ApiResponseReader.ReadClientAsync(response, ctx);
    }

}
