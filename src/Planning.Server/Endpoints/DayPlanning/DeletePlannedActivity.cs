using Planning.Shared.DayPlanning;

namespace Planning.Server.Endpoints.DayPlanning;

public class DeletePlannedActivity(IDayPlanningService dayPlanningService)
    : EndpointWithoutRequest<Result>
{
    public override void Configure()
    {
        Delete("/api/day-planning/activities/{plannedActivityId}");
    }

    public override Task<Result> ExecuteAsync(CancellationToken ct)
    {
        var plannedActivityId = Route<int>("plannedActivityId");
        return dayPlanningService.DeletePlannedActivityAsync(
            plannedActivityId, ct);
    }
}
