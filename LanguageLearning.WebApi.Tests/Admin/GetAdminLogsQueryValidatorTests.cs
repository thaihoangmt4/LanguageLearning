using LanguageLearning.WebApi.Features.Admin.Logs;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Admin;

public sealed class GetAdminLogsQueryValidatorTests
{
    private readonly GetAdminLogsQueryValidator _validator = new();

    [Fact]
    public async Task InvalidLevel_IsRejected()
    {
        var result = await _validator.ValidateAsync(
            new GetAdminLogsQuery(Level: "Critical"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task LimitOutsideRange_IsRejected(int limit)
    {
        var result = await _validator.ValidateAsync(
            new GetAdminLogsQuery(Limit: limit),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task InvertedDateRange_IsRejected()
    {
        var result = await _validator.ValidateAsync(
            new GetAdminLogsQuery(
                FromUtc: DateTimeOffset.UtcNow,
                ToUtc: DateTimeOffset.UtcNow.AddMinutes(-1)),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task SearchLongerThanMaximum_IsRejected()
    {
        var result = await _validator.ValidateAsync(
            new GetAdminLogsQuery(Search: new string('x', 201)),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }
}
