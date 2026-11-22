using Planning.Shared.DayPlanning;

namespace Planning.Services.DayPlanning;

public interface IDayPlanningService
{
    Task<Result<DayPlanningResponse>> GetDayAsync(
        DateOnly date, CancellationToken ctx = default);

    Task<Result<PlannedActivityDto>> CreateActivityAsync(
        CreatePlannedActivityRequest request, CancellationToken ctx = default);

    Task<Result> UpdateActivityAsync(
        int activityId, UpdatePlannedActivityRequest request, CancellationToken ctx = default);

    Task<Result> SetActivityCompletionAsync(
        int activityId, bool isCompleted, CancellationToken ctx = default);

    Task<Result> DeletePlannedActivityAsync(
        int plannedActivityId, CancellationToken ctx = default);
}
