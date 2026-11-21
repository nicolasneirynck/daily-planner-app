using Planning.Domain.PlannedActivities;

namespace Planning.Domain.Tests.PlannedActivities;

public class PlannedActivityShould
{
    [Fact]
    public void AcceptANameOf250Characters()
    {
        var name = new string('a', 250);

        var activity = new PlannedActivity
        {
            UserId = "user-1",
            Name = name,
            Date = new DateOnly(2026, 11, 14),
            TimeBlockId = 1
        };

        activity.Name.ShouldBe(name);
    }

    [Fact]
    public void RejectANameLongerThan250Characters()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new PlannedActivity
        {
            UserId = "user-1",
            Name = new string('a', 251),
            Date = new DateOnly(2026, 11, 14),
            TimeBlockId = 1
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(500)]
    public void AcceptAnOptionalDescriptionUpTo500Characters(int? length)
    {
        var description = length is null ? null : new string('a', length.Value);

        var activity = new PlannedActivity
        {
            UserId = "user-1",
            Name = "Read a book",
            Date = new DateOnly(2026, 11, 14),
            TimeBlockId = 1,
            Description = description
        };

        activity.Description.ShouldBe(description);
    }

    [Fact]
    public void RejectADescriptionLongerThan500Characters()
    {
        var description = new string('a', 501);

        Should.Throw<ArgumentOutOfRangeException>(() => new PlannedActivity
        {
            UserId = "user-1",
            Name = "Read a book",
            Date = new DateOnly(2026, 11, 14),
            TimeBlockId = 1,
            Description = description
        });
    }
}
