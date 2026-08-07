using System.Reflection;
using LanguageLearning.WebApi.Controllers;
using LanguageLearning.WebApi.Features.LearningProgress.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseEngine;

public sealed class LearningProgressApiTests
{
    [Fact]
    public void Controller_IsAuthenticatedAndExposesOnlyBackendDirectedLearningRoutes()
    {
        Assert.NotNull(typeof(LearningProgressController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/me", typeof(LearningProgressController).GetCustomAttribute<RouteAttribute>()!.Template);
        var templates = typeof(LearningProgressController).GetMethods()
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .Select(attribute => attribute.Template).ToHashSet();
        Assert.Contains("learning/continue", templates);
        Assert.Contains("learning/session", templates);
        Assert.Contains("learning/progress", templates);
        Assert.Contains("learning/history", templates);
        Assert.Contains("lesson-attempts/{lessonAttemptId:guid}/result", templates);
    }

    [Fact]
    public void SessionCommand_AcceptsNoLearnerSelectedCatalogIdentifiers()
    {
        Assert.DoesNotContain(typeof(StartLearningSessionCommand).GetProperties(),
            property => property.Name is "CourseId" or "UnitId" or "LessonId" or "ExerciseId");
    }
}
