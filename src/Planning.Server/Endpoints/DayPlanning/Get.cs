using Planning.Shared.DayPlanning;

namespace Planning.Server.Endpoints.DayPlanning;

public class Get(IDayPlanningService dayPlanningService)
    : EndpointWithoutRequest<Result<DayPlanningResponse>>
{
    public override void Configure()
    {
        Get("/api/day-planning/{date}");
    }

    public override Task<Result<DayPlanningResponse>> ExecuteAsync(
        CancellationToken ct)
    {
        var date = Route<DateOnly>("date");
        return dayPlanningService.GetDayAsync(date, ct);
    }
}
