using Planning.Shared.DayPlanning;

namespace Planning.Client.Pages;

internal static class DayPlanningTestData
{
    internal static TimeBlockDto Block(
        int order, string name, int startHour, int endHour) =>
        new()
        {
            Id = order,
            SortOrder = order,
            Name = name,
            StartTime = new TimeOnly(startHour, 0),
            EndTime = new TimeOnly(endHour, 0)
        };
}
