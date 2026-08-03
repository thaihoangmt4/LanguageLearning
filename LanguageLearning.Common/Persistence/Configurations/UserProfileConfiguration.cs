using LanguageLearning.Common.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

/// <summary>
/// Entity type configuration for <see cref="UserProfile"/>.
/// </summary>
public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_user_profiles_DailyGoalMinutes",
                "\"DailyGoalMinutes\" >= 5 AND \"DailyGoalMinutes\" <= 180"));

        builder.HasKey(up => up.Id);

        builder.Property(up => up.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(up => up.Username)
            .IsRequired(false)
            .HasMaxLength(30);

        builder.HasIndex(up => up.Username)
            .IsUnique();

        builder.Property(up => up.NativeLanguageCode)
            .IsRequired(false)
            .HasMaxLength(10);

        builder.Property(up => up.TimeZoneId)
            .IsRequired(false)
            .HasMaxLength(100);

        builder.Property(up => up.DailyGoalMinutes)
            .IsRequired()
            .HasDefaultValue(15);

        builder.Property(up => up.CreatedAt)
            .IsRequired();

        builder.Property(up => up.UpdatedAt)
            .IsRequired(false);

        builder.HasOne(up => up.User)
            .WithOne(u => u.UserProfile)
            .HasForeignKey<UserProfile>(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(up => up.UserId)
            .IsUnique();
    }
}
