namespace Planning.Domain.PlannedActivities;

public static class PlannedActivityRules
{
    public const string NameRequiredMessage = "Activity name is required.";

    public const int MaxNameLength = 250;
    public const string NameTooLongMessage =
        "Activity name must be 250 characters or fewer.";

    public const int MaxDescriptionLength = 500;
    public const string DescriptionTooLongMessage =
        "Description must be 500 characters or fewer.";
}
