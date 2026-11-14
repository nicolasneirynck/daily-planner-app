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
}
