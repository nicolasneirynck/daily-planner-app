namespace Planning.Shared.DayPlanning;

public interface IDayPlanningService
{
    Task<Result<DayPlanningResponse>> GetDayAsync(
        DateOnly date, CancellationToken ctx = default);

    Task<Result> CreateActivityAsync(
        CreatePlannedActivityRequest request, CancellationToken ctx = default);

    Task<Result> SetActivityCompletionAsync(
        int activityId, bool isCompleted, CancellationToken ctx = default);

    Task<Result> DeletePlannedActivityAsync(
        int plannedActivityId, CancellationToken ctx = default);
}
