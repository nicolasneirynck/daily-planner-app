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
            new DayPlanningResponse
            {
                Date = date,
                TimeBlocks =
                [
                    new TimeBlockDto
                    {
                        Id = 1,
                        Name = "Ochtend",
                        SortOrder = 1,
                        StartTime = new TimeOnly(6, 0),
                        EndTime = new TimeOnly(9, 0)
                    }
                ]
            }));
    }
}
