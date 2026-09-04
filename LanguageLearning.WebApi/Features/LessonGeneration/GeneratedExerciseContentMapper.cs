using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.WebApi.Features.ExerciseGeneration;

namespace LanguageLearning.WebApi.Features.LessonGeneration;

internal static class GeneratedExerciseContentMapper
{
    public static bool TryMap(
        GeneratedExercise exercise,
        IReadOnlyDictionary<Guid, ExerciseGenerationImageAsset> images,
        out object content)
    {
        switch (exercise.Type)
        {
            case ExerciseType.MultipleChoice:
            case ExerciseType.AudioMatching:
                var options = exercise.Options.Select(x => new ExerciseOption(Guid.NewGuid(), x.Trim())).ToArray();
                var correct = options.FirstOrDefault(x => string.Equals(x.Text, exercise.CorrectAnswer?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (correct is null) break;
                content = exercise.Type == ExerciseType.MultipleChoice
                    ? new MultipleChoiceContent(exercise.Question.Trim(), options, correct.Id, exercise.Explanation?.Trim())
                    : new AudioMatchingContent(exercise.PronunciationText!.Trim(), options, correct.Id, exercise.Explanation?.Trim());
                return true;
            case ExerciseType.Typing:
                content = new TypingContent(exercise.Question.Trim(), [exercise.CorrectAnswer!.Trim()], false, true,
                    exercise.Explanation?.Trim(), ExerciseLimits.MaximumTypingLength);
                return true;
            case ExerciseType.ImageMatching:
                if (exercise.ImageMatches is null || exercise.ImageMatches.Any(x => !images.ContainsKey(x.ImageMediaId))) break;
                var sources = exercise.ImageMatches.Select(x => new ImageMatchingSource(Guid.NewGuid(), x.ImageMediaId, images[x.ImageMediaId].AltText)).ToArray();
                var targets = exercise.ImageMatches.Select(x => new MatchingTarget(Guid.NewGuid(), x.Target.Trim())).ToArray();
                content = new ImageMatchingContent(sources, targets, sources.Zip(targets, (s, t) => new MatchPair(s.Id, t.Id)).ToArray(), exercise.Explanation?.Trim());
                return true;
            case ExerciseType.SentenceOrdering:
                var ordered = exercise.OrderedSegments!.Select(x => new SentenceToken(Guid.NewGuid(), x.Trim())).ToArray();
                var shown = ordered.Length == 2 ? ordered.Reverse().ToArray() : ordered.Skip(1).Append(ordered[0]).ToArray();
                content = new SentenceOrderingContent(exercise.Question.Trim(), shown, ordered.Select(x => x.Id).ToArray(), exercise.Explanation?.Trim());
                return true;
            case ExerciseType.Categorization:
                var categories = exercise.Categories!.Select(x => new ExerciseCategory(Guid.NewGuid(), x.Name.Trim())).ToArray();
                var items = new List<CategorizationItem>();
                var assignments = new List<CategoryAssignment>();
                for (var index = 0; index < categories.Length; index++)
                    foreach (var text in exercise.Categories[index].Items)
                    {
                        var item = new CategorizationItem(Guid.NewGuid(), text.Trim());
                        items.Add(item);
                        assignments.Add(new(item.Id, categories[index].Id));
                    }
                content = new CategorizationContent(items, categories, assignments, exercise.Explanation?.Trim());
                return true;
        }
        content = null!;
        return false;
    }
}
