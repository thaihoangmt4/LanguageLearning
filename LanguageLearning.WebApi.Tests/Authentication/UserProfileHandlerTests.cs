using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Features.Authentication.Commands;
using LanguageLearning.WebApi.Features.Authentication.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Authentication;

public sealed class UserProfileHandlerTests
{
    [Fact]
    public async Task GetProfile_CombinesProfileAndUserDataWithoutChangingResponse()
    {
        await using var db = Db();
        var profile = await SeedAsync(db);

        var result = await new GetMyProfileQuery.Handler(db).Handle(
            new() { UserId = profile.UserId },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("profile@test.local", result.Value.Email);
        Assert.Equal(profile.DisplayName, result.Value.DisplayName);
        Assert.Equal(profile.Username, result.Value.Username);
    }

    [Fact]
    public async Task UpdateProfile_PreservesValidationPersistenceAndResponseContract()
    {
        await using var db = Db();
        var profile = await SeedAsync(db);
        var handler = new UpdateUserProfileCommand.Handler(db, new UpdateUserProfileCommandValidator());

        var result = await handler.Handle(new()
        {
            UserId = profile.UserId,
            DisplayName = "  Updated Learner  ",
            Username = "  Updated_User  ",
            NativeLanguageCode = " VI ",
            TimeZoneId = "Asia/Bangkok",
            DailyGoalMinutes = 30
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("profile@test.local", result.Value.Email);
        Assert.Equal("Updated Learner", result.Value.DisplayName);
        Assert.Equal("updated_user", result.Value.Username);
        Assert.Equal("vi", result.Value.NativeLanguageCode);
        Assert.Equal(30, result.Value.DailyGoalMinutes);
    }

    private static async Task<UserProfile> SeedAsync(ApplicationDbContext db)
    {
        var user = new User
        {
            Email = "profile@test.local",
            FullName = "Profile Learner"
        };
        var profile = new UserProfile
        {
            User = user,
            UserId = user.Id,
            DisplayName = "Profile Learner",
            Username = "profile_user",
            NativeLanguageCode = "vi",
            TimeZoneId = "Asia/Bangkok",
            DailyGoalMinutes = 15
        };
        db.AddRange(user, profile);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return profile;
    }

    private static ApplicationDbContext Db() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
