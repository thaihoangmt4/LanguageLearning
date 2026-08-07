using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.Authentication.DTOs;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LanguageLearning.WebApi.Features.Authentication.Commands;

/// <summary>
/// Authenticates a user via their Google ID token.
/// Creates a new account if the user does not exist.
/// </summary>
public class GoogleLoginCommand : IRequest<Result<AuthResponse>>
{
    public string IdToken { get; set; }

    public class Handler : IRequestHandler<GoogleLoginCommand, Result<AuthResponse>>
    {
        private readonly IGoogleTokenVerifier _googleTokenVerifier;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly ApplicationDbContext _dbContext;
        private readonly TokenGenerationOptions _tokenOptions;
        private readonly IDefaultCourseResolver _defaultCourseResolver;

        public Handler(IGoogleTokenVerifier googleTokenVerifier, IJwtTokenGenerator jwtTokenGenerator, IRefreshTokenService refreshTokenService, ApplicationDbContext dbContext, TokenGenerationOptions tokenOptions, IDefaultCourseResolver defaultCourseResolver)
        {
            _googleTokenVerifier = googleTokenVerifier;
            _jwtTokenGenerator = jwtTokenGenerator;
            _refreshTokenService = refreshTokenService;
            _dbContext = dbContext;
            _tokenOptions = tokenOptions;
            _defaultCourseResolver = defaultCourseResolver;
        }

        public async Task<Result<AuthResponse>> Handle(GoogleLoginCommand command, CancellationToken cancellationToken)
        {
            // 1. Verify Google ID token
            var payload = await _googleTokenVerifier.VerifyAsync(command.IdToken, cancellationToken);

            if (payload is null)
                return Result<AuthResponse>.Failure("Invalid or expired Google ID token.");

            if (!payload.EmailVerified)
                return Result<AuthResponse>.Failure("Google account email is not verified.");

            // 2. Find existing user or create a new one
            var userResult = await FindOrCreateUserAsync(payload, cancellationToken);
            if (userResult.IsFailure)
                return Result<AuthResponse>.Failure(userResult.Error);

            var user = userResult.Value;

            // 3. Generate tokens
            var accessToken = GenerateAccessToken(user);
            var (rawToken, refreshTokenEntity) = _refreshTokenService.CreateRefreshToken(
                user.Id,
                _tokenOptions.RefreshTokenExpirationDays);

            // 4. Persist
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<AuthResponse>.Success(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = rawToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_tokenOptions.AccessTokenExpirationMinutes)
            });
        }

        private async Task<Result<User>> FindOrCreateUserAsync(GoogleTokenPayload payload,
            CancellationToken cancellationToken)
        {
            // Try to find by Google ID first
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.GoogleId == payload.Sub, cancellationToken);

            if (user is not null)
                return Result<User>.Success(user);

            // Fallback: try to match by email (for accounts created before GoogleId was added)
            user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == payload.Email, cancellationToken);

            if (user is not null)
            {
                // Link the Google ID to the existing account
                user.GoogleId = payload.Sub;
                return Result<User>.Success(user);
            }

            var defaultCourse = await _defaultCourseResolver.ResolveAsync(cancellationToken);
            if (defaultCourse.IsFailure)
                return Result<User>.Failure(defaultCourse.Error);

            var now = DateTime.UtcNow;

            // Create a new user
            user = new User
            {
                Email = payload.Email,
                FullName = payload.Name,
                GoogleId = payload.Sub,
                Role = "User",
                IsActive = true
            };

            var profile = new UserProfile
            {
                UserId = user.Id,
                User = user,
                DisplayName = payload.Name,
                Username = null,
                NativeLanguageCode = null,
                TimeZoneId = null,
                DailyGoalMinutes = 15
            };

            user.UserProfile = profile;

            var assignment = new UserCourseAssignment
            {
                UserId = user.Id,
                User = user,
                CourseId = defaultCourse.Value.Id,
                Course = defaultCourse.Value,
                Status = UserCourseAssignmentStatus.Assigned,
                AssignedAt = now
            };

            _dbContext.Users.Add(user);
            _dbContext.UserProfiles.Add(profile);
            _dbContext.UserCourseAssignments.Add(assignment);

            return Result<User>.Success(user);
        }

        private string GenerateAccessToken(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Role, user.Role)
            };

            return _jwtTokenGenerator.GenerateAccessToken(claims);
        }
    }
}
