namespace Planning.Domain.TimeBlocks;

public class TimeBlock : Entity
{
    private string _userId = string.Empty;
    public required string UserId
    {
        get => _userId;
        init => _userId = Guard.Against.NullOrWhiteSpace(value);
    }

    private string _name = string.Empty;
    public required string Name
    {
        get => _name;
        set => _name = Guard.Against.NullOrWhiteSpace(value);
    }

    public required int SortOrder { get; set; }
}