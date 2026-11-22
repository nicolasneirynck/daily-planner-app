using Planning.Services.DayPlanning;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Endpoints.DayPlanning;

public class Update(IDayPlanningService dayPlanningService)
    : Endpoint<UpdatePlannedActivityRequest, Result>
{
    public override void Configure()
    {
        Put("/api/day-planning/activities/{activityId}");
        Description(b => b.ClearDefaultProduces(200).Produces(204));
    }

    public override Task<Result> ExecuteAsync(
        UpdatePlannedActivityRequest request, CancellationToken ct)
    {
        var activityId = Route<int>("activityId");
        return dayPlanningService.UpdateActivityAsync(activityId, request, ct);
    }
}
