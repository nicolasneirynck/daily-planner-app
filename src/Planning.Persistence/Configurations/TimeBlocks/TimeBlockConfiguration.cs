using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planning.Domain.TimeBlocks;

namespace Planning.Persistence.Configurations.TimeBlocks;

internal class TimeBlockConfiguration : EntityConfiguration<TimeBlock>
{
    public override void Configure(EntityTypeBuilder<TimeBlock> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(250);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.SortOrder })
            .IsUnique();
    }
}
