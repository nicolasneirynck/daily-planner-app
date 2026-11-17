using Planning.Shared.DayPlanning;

namespace Planning.Server.Endpoints.DayPlanning;

public class SetCompletion(IDayPlanningService dayPlanningService)
    : Endpoint<SetActivityCompletionRequest, Result>
{
    public override void Configure()
    {
        Put("/api/day-planning/activities/{activityId}/completion");
    }

    public override Task<Result> ExecuteAsync(
        SetActivityCompletionRequest request, CancellationToken ct)
    {
        var activityId = Route<int>("activityId");
        return dayPlanningService.SetActivityCompletionAsync(
            activityId, request.IsCompleted, ct);
    }
}
