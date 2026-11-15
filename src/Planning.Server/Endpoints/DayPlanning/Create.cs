using Planning.Shared.DayPlanning;

namespace Planning.Server.Endpoints.DayPlanning;

public class Create(IDayPlanningService dayPlanningService)
    : Endpoint<CreatePlannedActivityRequest, Result>
{
    public override void Configure()
    {
        Post("/api/day-planning/activities");
    }

    public override Task<Result> ExecuteAsync(
        CreatePlannedActivityRequest request, CancellationToken ct)
    {
        return dayPlanningService.CreateActivityAsync(request, ct);
    }
}
