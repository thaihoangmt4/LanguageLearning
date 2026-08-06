namespace LanguageLearning.Common.ExerciseEngine.Models;

public sealed record MultipleChoiceAnswer(Guid SelectedOptionId);
public sealed record ImageMatchingAnswer(IReadOnlyList<MatchPair> Matches);
public sealed record AudioMatchingAnswer(Guid SelectedOptionId);
public sealed record TypingAnswer(string Text);
public sealed record SentenceOrderingAnswer(IReadOnlyList<Guid> OrderedTokenIds);
public sealed record CategorizationAnswer(IReadOnlyList<CategoryAssignment> Assignments);
public sealed record SpeakingAnswer(bool Acknowledged);
