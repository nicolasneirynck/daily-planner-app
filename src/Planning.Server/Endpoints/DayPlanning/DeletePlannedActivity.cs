using Planning.Services.DayPlanning;

namespace Planning.Server.Endpoints.DayPlanning;

public class DeletePlannedActivity(IDayPlanningService dayPlanningService)
    : EndpointWithoutRequest<Result>
{
    public override void Configure()
    {
        Delete("/api/day-planning/activities/{plannedActivityId}");
        Description(b => b.ClearDefaultProduces(200).Produces(204));
    }

    public override Task<Result> ExecuteAsync(CancellationToken ct)
    {
        var plannedActivityId = Route<int>("plannedActivityId");
        return dayPlanningService.DeletePlannedActivityAsync(
            plannedActivityId, ct);
    }
}
