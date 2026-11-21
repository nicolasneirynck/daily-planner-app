using Planning.Services.DayPlanning;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Endpoints.DayPlanning;

public class Get(IDayPlanningService dayPlanningService)
    : EndpointWithoutRequest<Result<DayPlanningResponse>>
{
    public override void Configure()
    {
        Get("/api/day-planning/{date}");
        Description(b => b.ClearDefaultProduces(200)
            .Produces<DayPlanningResponse>(200));
    }

    public override Task<Result<DayPlanningResponse>> ExecuteAsync(
        CancellationToken ct)
    {
        var date = Route<DateOnly>("date");
        return dayPlanningService.GetDayAsync(date, ct);
    }
}
