using LanguageLearning.Common.Entities.Settings;
using LanguageLearning.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    private static readonly DateTime InitialUpdatedAtUtc =
        new(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("system_settings", table =>
        {
            table.HasCheckConstraint(
                "CK_system_settings_Singleton",
                $"\"Id\" = '{SystemSettings.SingletonId}'::uuid");
            table.HasCheckConstraint(
                "CK_system_settings_MinimumLogLevel",
                "\"MinimumLogLevel\" IN ('Debug', 'Information', 'Warning', 'Error', 'Fatal')");
        });

        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.MinimumLogLevel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(settings => settings.ExerciseGenerationEnabled)
            .IsRequired()
            .HasDefaultValue(true);
        builder.Property(settings => settings.UpdatedAtUtc).IsRequired();
        builder.HasOne(settings => settings.UpdatedByUser)
            .WithMany()
            .HasForeignKey(settings => settings.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(new
        {
            Id = SystemSettings.SingletonId,
            MinimumLogLevel = SystemLogLevel.Information,
            ExerciseGenerationEnabled = true,
            UpdatedAtUtc = InitialUpdatedAtUtc,
            UpdatedByUserId = (Guid?)null
        });
    }
}
