using Planning.Services.DayPlanning;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Endpoints.DayPlanning;

public class GetActivity(IDayPlanningService dayPlanningService)
    : EndpointWithoutRequest<Result<PlannedActivityDetailsDto>>
{
    public override void Configure()
    {
        Get("/api/day-planning/activities/{activityId}");
        Description(b => b.ClearDefaultProduces(200)
            .Produces<PlannedActivityDetailsDto>(200));
    }

    public override Task<Result<PlannedActivityDetailsDto>> ExecuteAsync(
        CancellationToken ct)
    {
        var activityId = Route<int>("activityId");
        return dayPlanningService.GetActivityAsync(activityId, ct);
    }
}
