using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.ExerciseEngine.Evaluation;
using LanguageLearning.Common.Results;

namespace LanguageLearning.Common.ExerciseEngine.Validation;

internal static class ValidationResult
{
    public static Result Definition(bool valid) => valid ? Result.Success() : Result.Failure(ExerciseEngineErrors.InvalidDefinition);
    public static Result Answer(bool valid) => valid ? Result.Success() : Result.Failure(ExerciseEngineErrors.InvalidAnswer);
    public static bool Unique<T>(IEnumerable<T> values) where T : notnull => values.Distinct().Count() == values.Count();
}

public sealed class MultipleChoiceDefinitionValidator : IExerciseDefinitionValidator
{
    public ExerciseType ExerciseType => ExerciseType.MultipleChoice;
    public Result Validate(object value) => ValidationResult.Definition(value is MultipleChoiceContent c &&
        !string.IsNullOrWhiteSpace(c.Question) && c.Options is { Count: >= 2 and <= ExerciseLimits.MaximumOptions } &&
        c.Options.All(x => x.Id != Guid.Empty && !string.IsNullOrWhiteSpace(x.Text)) &&
        ValidationResult.Unique(c.Options.Select(x => x.Id)) && c.Options.Any(x => x.Id == c.CorrectOptionId));
}

public sealed class AudioMatchingDefinitionValidator : IExerciseDefinitionValidator
{
    public ExerciseType ExerciseType => ExerciseType.AudioMatching;
    public Result Validate(object value) => ValidationResult.Definition(value is AudioMatchingContent c && !string.IsNullOrWhiteSpace(c.PronunciationText) &&
        c.Options is { Count: >= 2 and <= ExerciseLimits.MaximumOptions } && c.Options.All(x => x.Id != Guid.Empty && !string.IsNullOrWhiteSpace(x.Text)) &&
        ValidationResult.Unique(c.Options.Select(x => x.Id)) && c.Options.Any(x => x.Id == c.CorrectOptionId));
}

public sealed class ImageMatchingDefinitionValidator : IExerciseDefinitionValidator
{
    public ExerciseType ExerciseType => ExerciseType.ImageMatching;
    public Result Validate(object value)
    {
        if (value is not ImageMatchingContent c || c.Sources is not { Count: > 0 and <= ExerciseLimits.MaximumMatchingPairs } ||
            c.Targets is not { Count: > 0 and <= ExerciseLimits.MaximumMatchingPairs } || c.CorrectMatches is null)
            return ValidationResult.Definition(false);
        var sourceIds = c.Sources.Select(x => x.Id).ToArray();
        var targetIds = c.Targets.Select(x => x.Id).ToHashSet();
        return ValidationResult.Definition(ValidationResult.Unique(sourceIds) && ValidationResult.Unique(c.Targets.Select(x => x.Id)) &&
            c.Sources.All(x => x.Id != Guid.Empty && x.ImageMediaId != Guid.Empty && !string.IsNullOrWhiteSpace(x.AltText)) &&
            c.Targets.All(x => x.Id != Guid.Empty && !string.IsNullOrWhiteSpace(x.Text)) &&
            c.CorrectMatches.Count == sourceIds.Length && ValidationResult.Unique(c.CorrectMatches.Select(x => x.SourceId)) &&
            c.CorrectMatches.All(x => sourceIds.Contains(x.SourceId) && targetIds.Contains(x.TargetId)));
    }
}

public sealed class TypingDefinitionValidator : IExerciseDefinitionValidator
{
    public ExerciseType ExerciseType => ExerciseType.Typing;
    public Result Validate(object value)
    {
        if (value is not TypingContent c || string.IsNullOrWhiteSpace(c.Prompt) ||
            c.AcceptedAnswers is not { Count: > 0 and <= ExerciseLimits.MaximumAcceptedAnswers } ||
            c.AcceptedAnswers.Any(string.IsNullOrWhiteSpace) || c.MaxLength is <= 0 or > ExerciseLimits.MaximumTypingLength)
            return ValidationResult.Definition(false);
        return ValidationResult.Definition(ValidationResult.Unique(c.AcceptedAnswers.Select(x => TypingNormalization.Normalize(x, c))));
    }
}

public sealed class SentenceOrderingDefinitionValidator : IExerciseDefinitionValidator
{
    public ExerciseType ExerciseType => ExerciseType.SentenceOrdering;
    public Result Validate(object value) => ValidationResult.Definition(value is SentenceOrderingContent c && !string.IsNullOrWhiteSpace(c.Prompt) &&
        c.Tokens is { Count: > 0 and <= ExerciseLimits.MaximumTokens } && c.CorrectOrder is not null && c.Tokens.All(x => x.Id != Guid.Empty && !string.IsNullOrWhiteSpace(x.Text)) &&
        ValidationResult.Unique(c.Tokens.Select(x => x.Id)) && c.CorrectOrder.Count == c.Tokens.Count &&
        ValidationResult.Unique(c.CorrectOrder) && c.CorrectOrder.ToHashSet().SetEquals(c.Tokens.Select(x => x.Id)));
}

public sealed class CategorizationDefinitionValidator : IExerciseDefinitionValidator
{
    public ExerciseType ExerciseType => ExerciseType.Categorization;
    public Result Validate(object value)
    {
        if (value is not CategorizationContent c || c.Items is not { Count: > 0 and <= ExerciseLimits.MaximumCategorizationItems } ||
            c.Categories is not { Count: > 0 and <= ExerciseLimits.MaximumCategories } || c.CorrectAssignments is null)
            return ValidationResult.Definition(false);
        var itemIds = c.Items.Select(x => x.Id).ToArray();
        var categoryIds = c.Categories.Select(x => x.Id).ToHashSet();
        return ValidationResult.Definition(ValidationResult.Unique(itemIds) && ValidationResult.Unique(c.Categories.Select(x => x.Id)) &&
            c.Items.All(x => x.Id != Guid.Empty && !string.IsNullOrWhiteSpace(x.Text)) &&
            c.Categories.All(x => x.Id != Guid.Empty && !string.IsNullOrWhiteSpace(x.Name)) &&
            c.CorrectAssignments.Count == itemIds.Length && ValidationResult.Unique(c.CorrectAssignments.Select(x => x.ItemId)) &&
            c.CorrectAssignments.All(x => itemIds.Contains(x.ItemId) && categoryIds.Contains(x.CategoryId)));
    }
}

public sealed class SpeakingDefinitionValidator : IExerciseDefinitionValidator
{
    public ExerciseType ExerciseType => ExerciseType.Speaking;
    public Result Validate(object value) => ValidationResult.Definition(value is SpeakingContent c &&
        !string.IsNullOrWhiteSpace(c.Prompt) && !string.IsNullOrWhiteSpace(c.ReferenceText));
}

public sealed class MultipleChoiceAnswerValidator : IExerciseAnswerValidator
{
    public ExerciseType ExerciseType => ExerciseType.MultipleChoice;
    public Result Validate(object content, object answer) => ValidationResult.Answer(content is MultipleChoiceContent c &&
        answer is MultipleChoiceAnswer a && c.Options.Any(x => x.Id == a.SelectedOptionId));
}
public sealed class AudioMatchingAnswerValidator : IExerciseAnswerValidator
{
    public ExerciseType ExerciseType => ExerciseType.AudioMatching;
    public Result Validate(object content, object answer) => ValidationResult.Answer(content is AudioMatchingContent c &&
        answer is AudioMatchingAnswer a && c.Options.Any(x => x.Id == a.SelectedOptionId));
}
public sealed class ImageMatchingAnswerValidator : IExerciseAnswerValidator
{
    public ExerciseType ExerciseType => ExerciseType.ImageMatching;
    public Result Validate(object content, object answer) => ValidationResult.Answer(content is ImageMatchingContent c && answer is ImageMatchingAnswer a &&
        a.Matches is not null && a.Matches.Count <= ExerciseLimits.MaximumMatchingPairs && a.Matches.Count == c.Sources.Count && ValidationResult.Unique(a.Matches.Select(x => x.SourceId)) &&
        ValidationResult.Unique(a.Matches.Select(x => x.TargetId)) && a.Matches.Select(x => x.SourceId).ToHashSet().SetEquals(c.Sources.Select(x => x.Id)) &&
        a.Matches.All(x => c.Targets.Any(t => t.Id == x.TargetId)));
}
public sealed class TypingAnswerValidator : IExerciseAnswerValidator
{
    public ExerciseType ExerciseType => ExerciseType.Typing;
    public Result Validate(object content, object answer) => ValidationResult.Answer(content is TypingContent c && answer is TypingAnswer a &&
        !string.IsNullOrWhiteSpace(a.Text) && a.Text.Length <= ExerciseLimits.MaximumTypingLength && (c.MaxLength is null || a.Text.Length <= c.MaxLength));
}
public sealed class SentenceOrderingAnswerValidator : IExerciseAnswerValidator
{
    public ExerciseType ExerciseType => ExerciseType.SentenceOrdering;
    public Result Validate(object content, object answer) => ValidationResult.Answer(content is SentenceOrderingContent c && answer is SentenceOrderingAnswer a &&
        a.OrderedTokenIds is not null && a.OrderedTokenIds.Count <= ExerciseLimits.MaximumTokens && a.OrderedTokenIds.Count == c.Tokens.Count && ValidationResult.Unique(a.OrderedTokenIds) &&
        a.OrderedTokenIds.ToHashSet().SetEquals(c.Tokens.Select(x => x.Id)));
}
public sealed class CategorizationAnswerValidator : IExerciseAnswerValidator
{
    public ExerciseType ExerciseType => ExerciseType.Categorization;
    public Result Validate(object content, object answer) => ValidationResult.Answer(content is CategorizationContent c && answer is CategorizationAnswer a &&
        a.Assignments is not null && a.Assignments.Count <= ExerciseLimits.MaximumCategorizationItems && a.Assignments.Count == c.Items.Count && ValidationResult.Unique(a.Assignments.Select(x => x.ItemId)) &&
        a.Assignments.Select(x => x.ItemId).ToHashSet().SetEquals(c.Items.Select(x => x.Id)) &&
        a.Assignments.All(x => c.Categories.Any(category => category.Id == x.CategoryId)));
}
public sealed class SpeakingAnswerValidator : IExerciseAnswerValidator
{
    public ExerciseType ExerciseType => ExerciseType.Speaking;
    public Result Validate(object content, object answer) => ValidationResult.Answer(content is SpeakingContent && answer is SpeakingAnswer { Acknowledged: true });
}

public sealed class ExerciseDefinitionValidatorResolver : IExerciseDefinitionValidatorResolver
{
    private readonly IReadOnlyDictionary<ExerciseType, IExerciseDefinitionValidator> _validators;
    public ExerciseDefinitionValidatorResolver(IEnumerable<IExerciseDefinitionValidator> validators) =>
        _validators = Registry.Build(validators, x => x.ExerciseType, "definition validator");
    public Result Validate(ExerciseType type, object content) => _validators.TryGetValue(type, out var validator)
        ? validator.Validate(content) : Result.Failure(ExerciseEngineErrors.InvalidDefinition);
}
public sealed class ExerciseAnswerValidatorResolver : IExerciseAnswerValidatorResolver
{
    private readonly IReadOnlyDictionary<ExerciseType, IExerciseAnswerValidator> _validators;
    public ExerciseAnswerValidatorResolver(IEnumerable<IExerciseAnswerValidator> validators) =>
        _validators = Registry.Build(validators, x => x.ExerciseType, "answer validator");
    public Result Validate(ExerciseType type, object content, object answer) => _validators.TryGetValue(type, out var validator)
        ? validator.Validate(content, answer) : Result.Failure(ExerciseEngineErrors.InvalidAnswer);
}

internal static class Registry
{
    public static IReadOnlyDictionary<ExerciseType, T> Build<T>(IEnumerable<T> values, Func<T, ExerciseType> key, string component)
    {
        var result = new Dictionary<ExerciseType, T>();
        foreach (var value in values)
            if (!result.TryAdd(key(value), value))
                throw new InvalidOperationException($"Duplicate exercise {component} registration for '{key(value)}'.");
        return result;
    }
}
