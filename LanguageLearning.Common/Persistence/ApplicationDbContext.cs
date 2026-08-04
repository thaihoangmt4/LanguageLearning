using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Common.Persistence;

/// <summary>
/// Primary database context for the Language Learning platform.
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Lesson> Lessons => Set<Lesson>();

    public DbSet<LessonSection> LessonSections => Set<LessonSection>();

    public DbSet<Vocabulary> Vocabularies => Set<Vocabulary>();

    public DbSet<LearningStep> LearningSteps => Set<LearningStep>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditableTimestamps();

        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditableTimestamps()
    {
        var entries = ChangeTracker
            .Entries<IAuditableEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }

            entry.Entity.UpdatedAt = now;
        }
    }
}
