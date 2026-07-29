using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Services;
using MediatR;

namespace LanguageLearning.WebApi.Features.Authentication.Commands;

/// <summary>
/// Revokes a refresh token, effectively logging the user out of that session.
/// Idempotent: succeeds even if the token is already revoked or not found.
/// </summary>
public class LogoutCommand : IRequest<Result>
{
    public string RefreshToken { get; }

    public class Handler : IRequestHandler<LogoutCommand, Result>
    {
        private readonly IRefreshTokenService _refreshTokenService;

        public Handler(IRefreshTokenService refreshTokenService)
        {
            _refreshTokenService = refreshTokenService;
        }

        public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            await _refreshTokenService.RevokeByRawTokenAsync(request.RefreshToken, cancellationToken);
            return Result.Success();
        }
    }
}