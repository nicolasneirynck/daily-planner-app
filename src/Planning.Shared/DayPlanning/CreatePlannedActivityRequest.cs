namespace Planning.Shared.DayPlanning;

public class CreatePlannedActivityRequest
{
    public required DateOnly Date { get; set; }
    public required int TimeBlockId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsImportant { get; set; }
}
