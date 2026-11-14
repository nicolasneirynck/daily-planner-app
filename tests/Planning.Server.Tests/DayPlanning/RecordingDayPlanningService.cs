using Ardalis.Result;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Tests.DayPlanning;

public sealed class RecordingDayPlanningService : IDayPlanningService
{
    public DateOnly? RequestedDate { get; private set; }

    public Task<Result<DayPlanningResponse>> GetDayAsync(
        DateOnly date, CancellationToken ctx = default)
    {
        RequestedDate = date;
        return Task.FromResult(Result.Success(
            new DayPlanningResponse { Date = date }));
    }
}
