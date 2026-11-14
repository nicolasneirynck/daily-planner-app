namespace Planning.Domain.Activities;

public class Activity : Entity
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
    
    public string? Description { get; set; }
}