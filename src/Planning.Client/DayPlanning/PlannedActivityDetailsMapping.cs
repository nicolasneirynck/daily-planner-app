using Planning.Shared.DayPlanning;

namespace Planning.Client.DayPlanning;

internal static class PlannedActivityDetailsMapping
{
    public static PlannedActivityDto ToPlannedActivity(
        this PlannedActivityDetailsDto details) => new()
    {
        Id = details.Id,
        Name = details.Name,
        Description = details.Description,
        IsImportant = details.IsImportant,
        IsCompleted = details.IsCompleted
    };
}
