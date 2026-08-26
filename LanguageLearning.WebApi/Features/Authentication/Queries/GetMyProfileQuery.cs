using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.Authentication.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.Authentication.Queries;

/// <summary>
/// Retrieves the authenticated user's learning profile.
/// </summary>
public sealed class GetMyProfileQuery : IRequest<Result<UserProfileResponse>>
{
    public Guid UserId { get; init; }

    public sealed class Handler : IRequestHandler<GetMyProfileQuery, Result<UserProfileResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public Handler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Result<UserProfileResponse>> Handle(
            GetMyProfileQuery request,
            CancellationToken cancellationToken)
        {
            var profile = await _dbContext.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(up => up.UserId == request.UserId, cancellationToken);

            if (profile is null)
                return Result<UserProfileResponse>.Failure("user_profile.not_found");

            var email = await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == request.UserId)
                .Select(user => user.Email)
                .SingleAsync(cancellationToken);

            return Result<UserProfileResponse>.Success(UserProfileResponse.From(profile, email));
        }
    }
}
