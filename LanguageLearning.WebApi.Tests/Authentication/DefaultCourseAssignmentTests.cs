using System.Security.Claims;
using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Features.Authentication.Commands;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Authentication;

public sealed class DefaultCourseAssignmentTests
{
    [Fact]
    public async Task NewUser_IsAssignedConfiguredPublishedCourse()
    {
        await using var db = Db();
        var arbitrary = Course("ARBITRARY", 0);
        var configured = Course("FOUNDATIONS", 9);
        AddValidLesson(db, configured);
        db.Add(arbitrary);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = new GoogleLoginCommand.Handler(new GoogleVerifier(), new JwtGenerator(), new RefreshTokens(), db,
            new TokenGenerationOptions(), new DefaultCourseResolver(db, new LearningOptions { DefaultCourseCode = "FOUNDATIONS" }));

        var result = await handler.Handle(new GoogleLoginCommand { IdToken = "valid" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var assignment = await db.UserCourseAssignments.Include(x => x.Course).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(configured.Id, assignment.CourseId);
        Assert.Equal(UserCourseAssignmentStatus.Assigned, assignment.Status);
        Assert.NotEqual(default, assignment.AssignedAt);
        Assert.Null(assignment.StartedAt);
        Assert.Single(await db.Users.ToListAsync(TestContext.Current.CancellationToken));
    }

    private static Course Course(string code, int order) => new()
    { Code = code, Title = code, DisplayOrder = order, IsPublished = true, CefrLevel = CefrLevel.A1 };

    private static void AddValidLesson(ApplicationDbContext db, Course course)
    {
        var unit = new Unit { Course = course, Code = "UNIT", Title = "Unit", DisplayOrder = 1 };
        var lesson = new Lesson { Unit = unit, Code = "LESSON", Title = "Lesson", DisplayOrder = 1,
            Status = LessonStatus.Published, DifficultyLevel = DifficultyLevel.Beginner, EstimatedDurationMinutes = 10 };
        var exercise = new Exercise { Lesson = lesson, Type = ExerciseType.Typing, Title = "Exercise", Instruction = "Type",
            Difficulty = DifficultyLevel.Beginner, DisplayOrder = 1, ContentJson = "{}", Version = 1, IsRequired = true, IsActive = true };
        db.AddRange(course, unit, lesson, exercise);
    }

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class GoogleVerifier : IGoogleTokenVerifier
    {
        public Task<GoogleTokenPayload?> VerifyAsync(string idToken, CancellationToken cancellationToken = default) =>
            Task.FromResult<GoogleTokenPayload?>(new() { Sub = "google-id", Email = "new@test.local", Name = "New User", EmailVerified = true });
    }
    private sealed class JwtGenerator : IJwtTokenGenerator
    {
        public string GenerateAccessToken(IEnumerable<Claim> claims) => "access-token";
    }
    private sealed class RefreshTokens : IRefreshTokenService
    {
        public (string rawToken, RefreshToken entity) CreateRefreshToken(Guid userId, int expirationDays) =>
            ("refresh-token", new RefreshToken { UserId = userId });
        public Task<RefreshToken?> GetByRawTokenAsync(string rawToken, CancellationToken cancellationToken = default) => Task.FromResult<RefreshToken?>(null);
        public Task RevokeByRawTokenAsync(string rawToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
