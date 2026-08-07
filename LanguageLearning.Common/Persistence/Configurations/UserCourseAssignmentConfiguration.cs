using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class UserCourseAssignmentConfiguration : IEntityTypeConfiguration<UserCourseAssignment>
{
    public void Configure(EntityTypeBuilder<UserCourseAssignment> builder)
    {
        builder.ToTable("user_course_assignments", table =>
        {
            table.HasCheckConstraint(
                "CK_user_course_assignments_Timestamps",
                "\"StartedAt\" IS NULL OR \"StartedAt\" >= \"AssignedAt\"");
            table.HasCheckConstraint(
                "CK_user_course_assignments_CompletedAt",
                "(\"Status\" = 'Completed' AND \"CompletedAt\" IS NOT NULL) OR (\"Status\" <> 'Completed' AND \"CompletedAt\" IS NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.AssignedAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);

        builder.HasOne(x => x.User).WithMany(x => x.CourseAssignments)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Course).WithMany(x => x.UserAssignments)
            .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Assigned', 'InProgress')")
            .HasDatabaseName("IX_user_course_assignments_UserId_Active");
        builder.HasIndex(x => new { x.UserId, x.Status, x.LastAccessedAt });
        builder.HasIndex(x => x.CourseId);
    }
}
