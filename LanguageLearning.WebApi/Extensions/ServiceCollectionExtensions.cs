using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using LanguageLearning.Common;
using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Constants;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.ExerciseEngine.Evaluation;
using LanguageLearning.Common.ExerciseEngine.PublicContent;
using LanguageLearning.Common.ExerciseEngine.Serialization;
using LanguageLearning.Common.ExerciseEngine.Validation;
using LanguageLearning.WebApi.Configuration;
using LanguageLearning.WebApi.Behaviors;
using LanguageLearning.WebApi.Middlewares;
using LanguageLearning.WebApi.Services;
using LanguageLearning.WebApi.Persistence;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LanguageLearning.WebApi.Extensions;

/// <summary>
/// Extension methods for registering all application services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all application services — EF Core, MediatR, FluentValidation, JWT auth,
    /// Serilog, OpenTelemetry, Health Checks, and Swagger.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var tokenOptions = BuildTokenOptions(configuration);
        var googleOptions = BuildGoogleOptions(configuration);
        var learningOptions = BuildLearningOptions(configuration);

        services.AddSingleton(tokenOptions);
        services.AddSingleton(googleOptions);
        services.AddSingleton(learningOptions);

        services.AddSingleton<ITokenHasher, TokenHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddScoped<ILearningPathResolver, SequentialLearningPathResolver>();
        services.AddScoped<IDefaultCourseResolver, DefaultCourseResolver>();
        services.AddScoped<ILearningSessionService, LearningSessionService>();
        services.AddScoped<IExerciseSubmissionService, ExerciseSubmissionService>();
        services.AddScoped<ExerciseEngineSeeder>();

        services.AddPostgres(configuration);
        services.AddExerciseEngine();
        services.AddMediatR();
        services.AddFluentValidation();
        services.AddFrontendCors(configuration);
        services.AddJwtAuthentication(configuration);
        services.AddApplicationAuthorization();
        services.AddHealthChecks(configuration);
        services.AddSwagger();
        services.AddGlobalExceptionHandler();

        services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
                manager.FeatureProviders.Add(new DevelopmentControllerFeatureProvider(environment)))
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddProblemDetails();

        return services;
    }

    private static IServiceCollection AddExerciseEngine(this IServiceCollection services)
    {
        services.AddSingleton<IExerciseContentSerializer, ExerciseContentSerializer>();
        services.AddSingleton<IExerciseAnswerSerializer, ExerciseAnswerSerializer>();

        services.AddSingleton<IExerciseDefinitionValidator, MultipleChoiceDefinitionValidator>();
        services.AddSingleton<IExerciseDefinitionValidator, ImageMatchingDefinitionValidator>();
        services.AddSingleton<IExerciseDefinitionValidator, AudioMatchingDefinitionValidator>();
        services.AddSingleton<IExerciseDefinitionValidator, TypingDefinitionValidator>();
        services.AddSingleton<IExerciseDefinitionValidator, SentenceOrderingDefinitionValidator>();
        services.AddSingleton<IExerciseDefinitionValidator, CategorizationDefinitionValidator>();
        services.AddSingleton<IExerciseDefinitionValidator, SpeakingDefinitionValidator>();
        services.AddSingleton<IExerciseDefinitionValidatorResolver, ExerciseDefinitionValidatorResolver>();

        services.AddSingleton<IExerciseAnswerValidator, MultipleChoiceAnswerValidator>();
        services.AddSingleton<IExerciseAnswerValidator, ImageMatchingAnswerValidator>();
        services.AddSingleton<IExerciseAnswerValidator, AudioMatchingAnswerValidator>();
        services.AddSingleton<IExerciseAnswerValidator, TypingAnswerValidator>();
        services.AddSingleton<IExerciseAnswerValidator, SentenceOrderingAnswerValidator>();
        services.AddSingleton<IExerciseAnswerValidator, CategorizationAnswerValidator>();
        services.AddSingleton<IExerciseAnswerValidator, SpeakingAnswerValidator>();
        services.AddSingleton<IExerciseAnswerValidatorResolver, ExerciseAnswerValidatorResolver>();

        services.AddSingleton<IExercisePublicContentMappingStrategy, MultipleChoicePublicMapper>();
        services.AddSingleton<IExercisePublicContentMappingStrategy, ImageMatchingPublicMapper>();
        services.AddSingleton<IExercisePublicContentMappingStrategy, AudioMatchingPublicMapper>();
        services.AddSingleton<IExercisePublicContentMappingStrategy, TypingPublicMapper>();
        services.AddSingleton<IExercisePublicContentMappingStrategy, SentenceOrderingPublicMapper>();
        services.AddSingleton<IExercisePublicContentMappingStrategy, CategorizationPublicMapper>();
        services.AddSingleton<IExercisePublicContentMappingStrategy, SpeakingPublicMapper>();
        services.AddSingleton<IExercisePublicContentMapper, ExercisePublicContentMapper>();

        services.AddSingleton<IExerciseEvaluationStrategy, MultipleChoiceEvaluator>();
        services.AddSingleton<IExerciseEvaluationStrategy, ImageMatchingEvaluator>();
        services.AddSingleton<IExerciseEvaluationStrategy, AudioMatchingEvaluator>();
        services.AddSingleton<IExerciseEvaluationStrategy, TypingEvaluator>();
        services.AddSingleton<IExerciseEvaluationStrategy, SentenceOrderingEvaluator>();
        services.AddSingleton<IExerciseEvaluationStrategy, CategorizationEvaluator>();
        services.AddSingleton<IExerciseEvaluationStrategy, SpeakingEvaluator>();
        services.AddSingleton<IExerciseEvaluatorResolver, ExerciseEvaluatorResolver>();

        return services;
    }

    /// <summary>
    /// Configures CORS for the frontend origins defined in configuration.
    /// </summary>
    private static IServiceCollection AddFrontendCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var corsSettings = configuration
            .GetSection(CorsSettings.SectionName)
            .Get<CorsSettings>()
            ?? throw new InvalidOperationException(
                $"Missing '{CorsSettings.SectionName}' configuration section.");

        if (corsSettings.AllowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                $"At least one origin must be configured in '{CorsSettings.SectionName}:AllowedOrigins'.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy(CorsSettings.FrontendPolicyName, policy =>
            {
                policy
                    .WithOrigins(corsSettings.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    /// <summary>
    /// Reads JWT token generation settings from configuration.
    /// </summary>
    private static TokenGenerationOptions BuildTokenOptions(IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>()!;

        return new TokenGenerationOptions
        {
            Secret = jwtSettings.Secret,
            Issuer = jwtSettings.Issuer,
            Audience = jwtSettings.Audience,
            AccessTokenExpirationMinutes = jwtSettings.ExpirationInMinutes,
            RefreshTokenExpirationDays = jwtSettings.RefreshTokenExpirationInDays
        };
    }

    /// <summary>
    /// Reads Google OAuth settings from configuration.
    /// </summary>
    private static GoogleAuthOptions BuildGoogleOptions(IConfiguration configuration)
    {
        var googleSettings = configuration
            .GetSection(GoogleSettings.SectionName)
            .Get<GoogleSettings>()!;

        return new GoogleAuthOptions
        {
            ClientId = googleSettings.ClientId
        };
    }

    private static LearningOptions BuildLearningOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(LearningOptions.SectionName).Get<LearningOptions>()
            ?? throw new InvalidOperationException($"Missing '{LearningOptions.SectionName}' configuration section.");
        if (string.IsNullOrWhiteSpace(options.DefaultCourseCode))
            throw new InvalidOperationException($"Missing '{LearningOptions.SectionName}:DefaultCourseCode' configuration value.");
        return options;
    }

    /// <summary>
    /// Configures Entity Framework Core with PostgreSQL using the connection string from configuration.
    /// </summary>
    private static IServiceCollection AddPostgres(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(AppConstants.ConnectionStringName);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }

    /// <summary>
    /// Registers MediatR from the Common assembly and all WebApi feature assemblies.
    /// </summary>
    private static IServiceCollection AddMediatR(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        return services;
    }

    /// <summary>
    /// Registers FluentValidation validators from all application assemblies.
    /// </summary>
    private static IServiceCollection AddFluentValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);

        return services;
    }

    /// <summary>
    /// Configures JWT Bearer authentication using settings from configuration.
    /// Only configures the JWT infrastructure; does not implement login or token generation.
    /// </summary>
    private static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>()!;

        var key = Encoding.UTF8.GetBytes(jwtSettings.Secret);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = true;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

        return services;
    }

    /// <summary>
    /// Configures role-based and policy-based authorization.
    /// </summary>
    private static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AppConstants.Policies.AdminOnly, policy =>
                policy.RequireRole(AppConstants.Roles.Admin))
            .AddPolicy(AppConstants.Policies.UserOnly, policy =>
                policy.RequireRole(AppConstants.Roles.User));

        return services;
    }

    /// <summary>
    /// Registers health checks for PostgreSQL connectivity and general application readiness.
    /// </summary>
    private static IServiceCollection AddHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(AppConstants.ConnectionStringName);

        services.AddHealthChecks()
            .AddNpgSql(
                connectionString: connectionString!,
                name: "postgresql",
                tags: ["db", "postgres"]);

        return services;
    }

    /// <summary>
    /// Configures Swagger/OpenAPI with JWT support for interactive API documentation.
    /// </summary>
    private static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new()
            {
                Title = "Language Learning Platform API",
                Version = "v1",
                Description = "AI-powered English learning platform."
            });

            var securityScheme = new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Enter your JWT token.",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            };

            options.AddSecurityDefinition("Bearer", securityScheme);

            options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
            {
                [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
            });
        });

        return services;
    }

    /// <summary>
    /// Registers the global exception handler.
    /// </summary>
    private static IServiceCollection AddGlobalExceptionHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Configures Serilog for structured logging.
    /// Called from Program.cs before builder.Build().
    /// </summary>
    public static void ConfigureSerilog(
        this IHostBuilder hostBuilder,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console();

        if (!environment.IsDevelopment())
        {
            loggerConfig.WriteTo.File(
                path: Path.Combine("logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = loggerConfig.CreateLogger();

        hostBuilder.UseSerilog();
    }

    /// <summary>
    /// Configures OpenTelemetry tracing and metrics with the configured exporters.
    /// </summary>
    public static IServiceCollection AddOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var serviceName = configuration.GetValue<string>("OpenTelemetry:ServiceName") ?? "language-learning-api";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                metrics.AddOtlpExporter();
            });

        return services;
    }
}
