using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.Authentication.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.Authentication.Queries;

/// <summary>
/// Retrieves the currently authenticated user's profile.
/// </summary>
public class GetCurrentUserQuery : IRequest<Result<UserResponse>>
{
    public string UserId { get; set; }

    public class Handler : IRequestHandler<GetCurrentUserQuery, Result<UserResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public Handler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Result<UserResponse>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id.ToString() == request.UserId, cancellationToken);

            if (user == default)
                return Result<UserResponse>.Failure("User not found.");

            return Result<UserResponse>.Success(new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive
            });
        }
    }
}