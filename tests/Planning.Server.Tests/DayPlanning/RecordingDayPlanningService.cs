using Ardalis.Result;
using Planning.Services.DayPlanning;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Tests.DayPlanning;

public sealed class RecordingDayPlanningService : IDayPlanningService
{
    public DateOnly? RequestedDate { get; private set; }
    public CreatePlannedActivityRequest? CreatedActivity { get; private set; }
    public Func<CreatePlannedActivityRequest, Result<PlannedActivityDto>>? CreateActivityOverride { get; set; }
    public int? UpdatedActivityId { get; private set; }
    public bool? UpdatedIsCompleted { get; private set; }
    public int? DeletedPlannedActivityId { get; private set; }

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

    public Task<Result<PlannedActivityDto>> CreateActivityAsync(
        CreatePlannedActivityRequest request, CancellationToken ctx = default)
    {
        CreatedActivity = request;
        return Task.FromResult(
            CreateActivityOverride?.Invoke(request) ?? Result.Success(
                new PlannedActivityDto
                {
                    Id = 42,
                    Name = request.Name,
                    IsCompleted = false
                }));
    }

    public Task<Result> SetActivityCompletionAsync(
        int activityId, bool isCompleted, CancellationToken ctx = default)
    {
        UpdatedActivityId = activityId;
        UpdatedIsCompleted = isCompleted;
        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeletePlannedActivityAsync(
        int plannedActivityId, CancellationToken ctx = default)
    {
        DeletedPlannedActivityId = plannedActivityId;
        return Task.FromResult(Result.Success());
    }
}
