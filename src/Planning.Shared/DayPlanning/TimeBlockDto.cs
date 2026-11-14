namespace Planning.Shared.DayPlanning;

public class TimeBlockDto
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public required int SortOrder { get; set; }
    public required TimeOnly StartTime { get; set; }
    public required TimeOnly EndTime { get; set; }
    public IReadOnlyList<PlannedActivityDto> Activities { get; set; } = [];
}
