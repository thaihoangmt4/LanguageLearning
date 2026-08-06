using System.Globalization;
using System.Text;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.ExerciseEngine.Validation;
using LanguageLearning.Common.Results;

namespace LanguageLearning.Common.ExerciseEngine.Evaluation;

public static class TypingNormalization
{
    public static string Normalize(string value, TypingContent content)
    {
        var normalized = value.Trim();
        if (content.IgnorePunctuation)
            normalized = new string(normalized.Where(character => !char.IsPunctuation(character)).ToArray());
        return content.CaseSensitive ? normalized : normalized.ToUpper(CultureInfo.InvariantCulture);
    }
}

public abstract class ExerciseEvaluationStrategy<TContent, TAnswer> : IExerciseEvaluator<TContent, TAnswer>, IExerciseEvaluationStrategy
{
    public abstract ExerciseType ExerciseType { get; }
    public abstract ExerciseEvaluationResult Evaluate(TContent content, TAnswer answer);
    ExerciseEvaluationResult IExerciseEvaluationStrategy.Evaluate(object content, object answer) =>
        Evaluate((TContent)content, (TAnswer)answer);
}

public sealed class MultipleChoiceEvaluator : ExerciseEvaluationStrategy<MultipleChoiceContent, MultipleChoiceAnswer>
{
    public override ExerciseType ExerciseType => ExerciseType.MultipleChoice;
    public override ExerciseEvaluationResult Evaluate(MultipleChoiceContent content, MultipleChoiceAnswer answer)
    {
        var correct = answer.SelectedOptionId == content.CorrectOptionId;
        return Binary(correct, content.Explanation, content.CorrectOptionId);
    }
    internal static ExerciseEvaluationResult Binary(bool correct, string? explanation, object expected) => new(
        correct ? EvaluationStatus.Correct : EvaluationStatus.Incorrect, correct ? 100m : 0m,
        correct ? "Correct." : "Incorrect.", explanation, expected, null);
}

public sealed class AudioMatchingEvaluator : ExerciseEvaluationStrategy<AudioMatchingContent, AudioMatchingAnswer>
{
    public override ExerciseType ExerciseType => ExerciseType.AudioMatching;
    public override ExerciseEvaluationResult Evaluate(AudioMatchingContent content, AudioMatchingAnswer answer) =>
        MultipleChoiceEvaluator.Binary(answer.SelectedOptionId == content.CorrectOptionId, content.Explanation, content.CorrectOptionId);
}

public sealed class TypingEvaluator : ExerciseEvaluationStrategy<TypingContent, TypingAnswer>
{
    public override ExerciseType ExerciseType => ExerciseType.Typing;
    public override ExerciseEvaluationResult Evaluate(TypingContent content, TypingAnswer answer)
    {
        var submitted = TypingNormalization.Normalize(answer.Text, content);
        var correct = content.AcceptedAnswers.Any(x => TypingNormalization.Normalize(x, content) == submitted);
        return MultipleChoiceEvaluator.Binary(correct, content.Explanation, content.AcceptedAnswers[0]);
    }
}

public sealed class SentenceOrderingEvaluator : ExerciseEvaluationStrategy<SentenceOrderingContent, SentenceOrderingAnswer>
{
    public override ExerciseType ExerciseType => ExerciseType.SentenceOrdering;
    public override ExerciseEvaluationResult Evaluate(SentenceOrderingContent content, SentenceOrderingAnswer answer) =>
        MultipleChoiceEvaluator.Binary(answer.OrderedTokenIds.SequenceEqual(content.CorrectOrder), content.Explanation, content.CorrectOrder);
}

public sealed class ImageMatchingEvaluator : ExerciseEvaluationStrategy<ImageMatchingContent, ImageMatchingAnswer>
{
    public override ExerciseType ExerciseType => ExerciseType.ImageMatching;
    public override ExerciseEvaluationResult Evaluate(ImageMatchingContent content, ImageMatchingAnswer answer)
    {
        var expected = content.CorrectMatches.ToDictionary(x => x.SourceId, x => x.TargetId);
        var details = answer.Matches.Select(x => new ItemEvaluationDetail(x.SourceId, x.TargetId, expected[x.SourceId], expected[x.SourceId] == x.TargetId)).ToArray();
        return Percentage(details, content.Explanation, content.CorrectMatches);
    }
    internal static ExerciseEvaluationResult Percentage(IReadOnlyList<ItemEvaluationDetail> details, string? explanation, object expected)
    {
        var correct = details.Count(x => x.IsCorrect);
        var status = correct == details.Count ? EvaluationStatus.Correct : correct == 0 ? EvaluationStatus.Incorrect : EvaluationStatus.PartiallyCorrect;
        var score = Math.Round(correct * 100m / details.Count, 2);
        return new(status, score, status switch
        {
            EvaluationStatus.Correct => "Correct.",
            EvaluationStatus.Incorrect => "Incorrect.",
            _ => "Partially correct."
        }, explanation, expected, details);
    }
}

public sealed class CategorizationEvaluator : ExerciseEvaluationStrategy<CategorizationContent, CategorizationAnswer>
{
    public override ExerciseType ExerciseType => ExerciseType.Categorization;
    public override ExerciseEvaluationResult Evaluate(CategorizationContent content, CategorizationAnswer answer)
    {
        var expected = content.CorrectAssignments.ToDictionary(x => x.ItemId, x => x.CategoryId);
        var details = answer.Assignments.Select(x => new ItemEvaluationDetail(x.ItemId, x.CategoryId, expected[x.ItemId], expected[x.ItemId] == x.CategoryId)).ToArray();
        return ImageMatchingEvaluator.Percentage(details, content.Explanation, content.CorrectAssignments);
    }
}

public sealed class SpeakingEvaluator : ExerciseEvaluationStrategy<SpeakingContent, SpeakingAnswer>
{
    public override ExerciseType ExerciseType => ExerciseType.Speaking;
    public override ExerciseEvaluationResult Evaluate(SpeakingContent content, SpeakingAnswer answer) => new(
        EvaluationStatus.NotEvaluated, null, "Automatic speaking evaluation is not available yet.", null, null, null);
}

public sealed class ExerciseEvaluatorResolver : IExerciseEvaluatorResolver
{
    private readonly IReadOnlyDictionary<ExerciseType, IExerciseEvaluationStrategy> _evaluators;
    public ExerciseEvaluatorResolver(IEnumerable<IExerciseEvaluationStrategy> evaluators) =>
        _evaluators = Registry.Build(evaluators, x => x.ExerciseType, "evaluator");
    public Result<ExerciseEvaluationResult> Evaluate(ExerciseType type, object content, object answer) =>
        _evaluators.TryGetValue(type, out var evaluator)
            ? Result<ExerciseEvaluationResult>.Success(evaluator.Evaluate(content, answer))
            : Result<ExerciseEvaluationResult>.Failure(ExerciseEngineErrors.EvaluatorNotRegistered);
}
