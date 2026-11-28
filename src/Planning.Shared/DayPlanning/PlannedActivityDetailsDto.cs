namespace Planning.Shared.DayPlanning;

public class PlannedActivityDetailsDto
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required DateOnly Date { get; set; }
    public required int TimeBlockId { get; set; }
    public required string TimeBlockName { get; set; }
    public required TimeOnly StartTime { get; set; }
    public required TimeOnly EndTime { get; set; }
    public bool IsImportant { get; set; }
    public bool IsCompleted { get; set; }
}
