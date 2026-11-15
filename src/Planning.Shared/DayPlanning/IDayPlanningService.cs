namespace Planning.Shared.DayPlanning;

public interface IDayPlanningService
{
    Task<Result<DayPlanningResponse>> GetDayAsync(
        DateOnly date, CancellationToken ctx = default);

    Task<Result> CreateActivityAsync(
        CreatePlannedActivityRequest request, CancellationToken ctx = default);
}
