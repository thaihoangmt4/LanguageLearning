using FluentValidation;
using MediatR;

namespace LanguageLearning.WebApi.Features.Admin.Logs;

public sealed record GetAdminLogsQuery(
    string? Level = null,
    string? Search = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Limit = 100,
    DateTimeOffset? BeforeUtc = null) : IRequest<AdminLogPageResponse>;

public sealed class GetAdminLogsQueryValidator : AbstractValidator<GetAdminLogsQuery>
{
    private static readonly string[] ValidLevels =
        ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    public GetAdminLogsQueryValidator()
    {
        RuleFor(query => query.Level)
            .Must(level => level is null || ValidLevels.Contains(level, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Level must be one of: {string.Join(", ", ValidLevels)}.");
        RuleFor(query => query.Search)
            .MaximumLength(200);
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, 200);
        RuleFor(query => query)
            .Must(query => query.FromUtc is null || query.ToUtc is null || query.FromUtc <= query.ToUtc)
            .WithMessage("FromUtc must be earlier than or equal to ToUtc.");
    }
}

public sealed class GetAdminLogsQueryHandler(ILogQueryService logQueryService)
    : IRequestHandler<GetAdminLogsQuery, AdminLogPageResponse>
{
    public Task<AdminLogPageResponse> Handle(
        GetAdminLogsQuery request,
        CancellationToken cancellationToken) =>
        logQueryService.QueryAsync(
            new(
                request.Level,
                request.Search,
                request.FromUtc,
                request.ToUtc,
                request.BeforeUtc,
                request.Limit),
            cancellationToken);
}
