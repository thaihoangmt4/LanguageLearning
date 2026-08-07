using LanguageLearning.WebApi.Features.ExerciseGeneration;

namespace LanguageLearning.WebApi.Infrastructure.DeepSeek;

public sealed class DeepSeekGenerationException : ExerciseGenerationException
{
    public DeepSeekGenerationException(string message) : base(message) { }
    public DeepSeekGenerationException(string message, Exception innerException) : base(message, innerException) { }
}
