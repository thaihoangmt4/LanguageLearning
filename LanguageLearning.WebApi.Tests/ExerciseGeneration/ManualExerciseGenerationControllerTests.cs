using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Controllers;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using LanguageLearning.WebApi.Features.ExerciseGeneration.Commands;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseGeneration;

public sealed class ManualExerciseGenerationControllerTests
{
    [Fact]
    public async Task DisabledGeneration_ReturnsConflictBusinessResponse()
    {
        var controller = new TestExerciseGenerationController(new StubSender(
            Result<GenerateExercisesResult>.Failure(ExerciseGenerationSettingsErrors.Disabled)))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = await controller.GenerateExercises(TestContext.Current.CancellationToken);

        var conflict = Assert.IsType<ConflictObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(
            "AI exercise generation is disabled.",
            Assert.IsType<ProblemDetails>(conflict.Value).Title);
    }

    private sealed class StubSender(object response) : ISender
    {
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((TResponse)response);

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest => Task.CompletedTask;

        public Task<object?> Send(
            object request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<object?>(response);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default) => Empty<object?>();

        private static async IAsyncEnumerable<T> Empty<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
