namespace Planning.Domain.PlannedActivities;

public class PlannedActivity : Entity
{
    private string _userId = string.Empty;
    public required string UserId
    {
        get => _userId;
        init => _userId = Guard.Against.NullOrWhiteSpace(value);
    }

    public int? ActivityId { get; set; }

    private string _name = string.Empty;
    public required string Name
    {
        get => _name;
        set
        {
            var name = Guard.Against.NullOrWhiteSpace(value);
            if (name.Length > PlannedActivityRules.MaxNameLength)
                throw new ArgumentOutOfRangeException(
                    nameof(Name), PlannedActivityRules.NameTooLongMessage);

            _name = name;
        }
    }

    private string? _description;
    public string? Description
    {
        get => _description;
        set
        {
            if (value?.Length > PlannedActivityRules.MaxDescriptionLength)
                throw new ArgumentOutOfRangeException(
                    nameof(Description), PlannedActivityRules.DescriptionTooLongMessage);

            _description = value;
        }
    }

    public required DateOnly Date { get; set; }
    public required int TimeBlockId { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsImportant { get; set; }
}