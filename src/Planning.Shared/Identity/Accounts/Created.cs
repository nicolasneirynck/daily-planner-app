namespace Planning.Shared.Identity.Accounts;

public static partial class AccountResponse
{
    public class Created
    {
        public required string Id { get; init; }
        public required string Email { get; init; }
    }
}
