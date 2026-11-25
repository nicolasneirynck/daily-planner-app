using Planning.Client.Http;
using Planning.Shared.DayPlanning;

namespace Planning.Client.DayPlanning;

public interface IDayPlanningClient
{
    Task<ClientResult<DayPlanningResponse>> GetDayAsync(DateOnly date, CancellationToken ctx = default);

    Task<ClientResult<PlannedActivityDetailsDto>> GetActivityAsync(int activityId, CancellationToken ctx = default);

    Task<ClientResult> CreateActivityAsync(CreatePlannedActivityRequest request, CancellationToken ctx = default);

    Task<ClientResult> UpdateActivityAsync(
        int activityId,
        UpdatePlannedActivityRequest request,
        CancellationToken ctx = default);

    Task<ClientResult> SetActivityCompletionAsync(int activityId, bool isCompleted, CancellationToken ctx = default);

    Task<ClientResult> DeletePlannedActivityAsync(int plannedActivityId, CancellationToken ctx = default);
}
