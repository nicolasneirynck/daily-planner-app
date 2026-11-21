using Planning.Services.DayPlanning;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Endpoints.DayPlanning;

public class Create(IDayPlanningService dayPlanningService)
    : Endpoint<CreatePlannedActivityRequest, Result<PlannedActivityDto>>
{
    public override void Configure()
    {
        Post("/api/day-planning/activities");
        Description(b => b.ClearDefaultProduces(200) // verwijdert de automatisch gedocumenteerde 200-respons.
            .Produces<PlannedActivityDto>(201)); // documenteert 201 Created met een PlannedActivityDto als JSON-body.
        Summary(s => s.ResponseHeaders.Add(new ResponseHeader(201, "Location") // documenteert dat een 201-respons een Location-header heeft met de URI van de aangemaakte activiteit.
        {
            Description = "URI of the created planned activity."
        }));
    }

    public override async Task<Result<PlannedActivityDto>> ExecuteAsync(
        CreatePlannedActivityRequest request, CancellationToken ct)
    {
        var result = await dayPlanningService.CreateActivityAsync(request, ct);
        if (result.Status != ResultStatus.Ok)
            return result;

        return Result.Created(result.Value,
            $"/api/day-planning/activities/{result.Value.Id}");
    }
}
