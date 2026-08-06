namespace LanguageLearning.Common.ExerciseEngine.Models;

public sealed record ExerciseOption(Guid Id, string Text);
public sealed record MultipleChoiceContent(string Question, IReadOnlyList<ExerciseOption> Options, Guid CorrectOptionId, string? Explanation);

public sealed record ImageMatchingSource(Guid Id, Guid ImageMediaId, string AltText);
public sealed record MatchingTarget(Guid Id, string Text);
public sealed record MatchPair(Guid SourceId, Guid TargetId);
public sealed record ImageMatchingContent(IReadOnlyList<ImageMatchingSource> Sources, IReadOnlyList<MatchingTarget> Targets, IReadOnlyList<MatchPair> CorrectMatches, string? Explanation);

public sealed record AudioMatchingContent(string PronunciationText, IReadOnlyList<ExerciseOption> Options, Guid CorrectOptionId, string? Explanation);
public sealed record TypingContent(string Prompt, IReadOnlyList<string> AcceptedAnswers, bool CaseSensitive, bool IgnorePunctuation, string? Explanation, int? MaxLength);

public sealed record SentenceToken(Guid Id, string Text);
public sealed record SentenceOrderingContent(string Prompt, IReadOnlyList<SentenceToken> Tokens, IReadOnlyList<Guid> CorrectOrder, string? Explanation);

public sealed record CategorizationItem(Guid Id, string Text);
public sealed record ExerciseCategory(Guid Id, string Name);
public sealed record CategoryAssignment(Guid ItemId, Guid CategoryId);
public sealed record CategorizationContent(IReadOnlyList<CategorizationItem> Items, IReadOnlyList<ExerciseCategory> Categories, IReadOnlyList<CategoryAssignment> CorrectAssignments, string? Explanation);

public sealed record SpeakingContent(string Prompt, string ReferenceText, Guid? ReferenceAudioMediaId);
