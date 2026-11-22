namespace Planning.Shared.DayPlanning;

public class PlannedActivityDto
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsImportant { get; set; }
}
