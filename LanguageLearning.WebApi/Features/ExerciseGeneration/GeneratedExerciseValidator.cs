using FluentValidation;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;

namespace LanguageLearning.WebApi.Features.ExerciseGeneration;

public sealed class GeneratedExerciseValidator : AbstractValidator<GeneratedExercise>
{
    private static readonly ExerciseType[] SupportedTypes = Enum.GetValues<ExerciseType>();

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

        When(x => x.Type == ExerciseType.AudioMatching, () =>
        {
            RuleFor(x => x.PronunciationText).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Options)
                .NotNull()
                .Must(x => x is { Count: >= 2 and <= 8 })
                .Must(ValidUniqueText);
            RuleFor(x => x.CorrectAnswer)
                .NotEmpty()
                .Must((exercise, answer) => exercise.Options.Any(option =>
                    string.Equals(option.Trim(), answer?.Trim(), StringComparison.OrdinalIgnoreCase)));
        });

        When(x => x.Type == ExerciseType.ImageMatching, () =>
        {
            RuleFor(x => x.ImageMatches)
                .NotNull()
                .Must(x => x is { Count: >= 2 and <= 8 })
                .Must(x => x is not null &&
                    x.All(match => match.ImageMediaId != Guid.Empty &&
                        !string.IsNullOrWhiteSpace(match.Target) && match.Target.Length <= 500) &&
                    x.Select(match => match.ImageMediaId).Distinct().Count() == x.Count &&
                    x.Select(match => match.Target.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == x.Count);
        });

        When(x => x.Type == ExerciseType.SentenceOrdering, () =>
        {
            RuleFor(x => x.OrderedSegments)
                .NotNull()
                .Must(x => x is { Count: >= 2 and <= 20 })
                .Must(ValidUniqueTextOrNecessaryRepetition);
        });

        When(x => x.Type == ExerciseType.Categorization, () =>
        {
            RuleFor(x => x.Categories)
                .NotNull()
                .Must(x => x is { Count: >= 2 and <= 4 })
                .Must(ValidCategories);
        });

        When(x => x.Type == ExerciseType.Speaking, () =>
        {
            RuleFor(x => x.ReferenceText).NotEmpty().MaximumLength(500);
        });
    }

    private static bool ValidUniqueText(IReadOnlyList<string> values) =>
        values.All(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 500) &&
        values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Count;

    private static bool ValidUniqueTextOrNecessaryRepetition(IReadOnlyList<string>? values) =>
        values is not null && values.All(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 100);

    private static bool ValidCategories(IReadOnlyList<GeneratedCategory>? categories)
    {
        if (categories is null ||
            categories.Any(category => string.IsNullOrWhiteSpace(category.Name) ||
                category.Name.Length > 200 || category.Items is not { Count: >= 2 and <= 6 } ||
                category.Items.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 500)) ||
            categories.Select(category => category.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != categories.Count)
            return false;

        var items = categories.SelectMany(category => category.Items).Select(item => item.Trim()).ToArray();
        return items.Distinct(StringComparer.OrdinalIgnoreCase).Count() == items.Length;
    }
}
