namespace Planning.Shared.DayPlanning;

public class DayPlanningResponse
{
    public required DateOnly Date { get; set; }
    public IReadOnlyList<TimeBlockDto> TimeBlocks { get; set; } = [];
}
