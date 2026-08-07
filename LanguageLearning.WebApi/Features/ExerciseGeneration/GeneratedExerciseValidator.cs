using FluentValidation;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;

namespace LanguageLearning.WebApi.Features.ExerciseGeneration;

public sealed class GeneratedExerciseValidator : AbstractValidator<GeneratedExercise>
{
    private static readonly ExerciseType[] SupportedTypes =
        [ExerciseType.MultipleChoice, ExerciseType.Typing];

    public GeneratedExerciseValidator()
    {
        RuleFor(x => x.Type).Must(SupportedTypes.Contains);
        RuleFor(x => x.Question).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Explanation).MaximumLength(2000);

        When(x => x.Type == ExerciseType.MultipleChoice, () =>
        {
            RuleFor(x => x.Options)
                .NotNull()
                .Must(x => x is { Count: >= 2 and <= ExerciseLimits.MaximumOptions })
                .Must(x => x.All(option => !string.IsNullOrWhiteSpace(option) && option.Length <= 500))
                .Must(x => x.Distinct(StringComparer.OrdinalIgnoreCase).Count() == x.Count);
            RuleFor(x => x.CorrectAnswer)
                .NotEmpty()
                .Must((exercise, answer) => exercise.Options.Any(option =>
                    string.Equals(option.Trim(), answer?.Trim(), StringComparison.OrdinalIgnoreCase)));
        });

        When(x => x.Type == ExerciseType.Typing, () =>
        {
            RuleFor(x => x.CorrectAnswer).NotEmpty().MaximumLength(ExerciseLimits.MaximumTypingLength);
        });
    }
}
