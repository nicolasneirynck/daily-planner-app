namespace Planning.Shared.Identity.Roles;

public static partial class RoleResponse
{
    public class Created
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
    }
}
