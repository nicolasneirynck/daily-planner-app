using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planning.Domain.Activities;
using Planning.Domain.PlannedActivities;
using Planning.Domain.TimeBlocks;

namespace Planning.Persistence.Configurations.PlannedActivities;

internal class PlannedActivityConfiguration : EntityConfiguration<PlannedActivity>
{
    public override void Configure(EntityTypeBuilder<PlannedActivity> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(PlannedActivityRules.MaxNameLength);
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.IsCompleted).HasDefaultValue(false);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Activity>()
            .WithMany()
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<TimeBlock>()
            .WithMany()
            // samengestelde FK -> PlannedActivity (TimeBlockId, UserId) -> TimeBlock (Id, UserId)
            .HasForeignKey(x => new { x.TimeBlockId, x.UserId })
            // HasPrincipal key -> geeft aan over welke kolommen van TimeBlockID het gaat
            .HasPrincipalKey(x => new { x.Id, x.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.Date });
    }
}
