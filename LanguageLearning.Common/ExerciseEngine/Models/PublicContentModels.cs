namespace LanguageLearning.Common.ExerciseEngine.Models;

public sealed record MultipleChoicePublicContent(string Question, IReadOnlyList<ExerciseOption> Options);
public sealed record ImageMatchingPublicContent(IReadOnlyList<ImageMatchingSource> Sources, IReadOnlyList<MatchingTarget> Targets);
public sealed record AudioMatchingPublicContent(string PronunciationText, IReadOnlyList<ExerciseOption> Options);
public sealed record TypingPublicContent(string Prompt, int? MaxLength);
public sealed record SentenceOrderingPublicContent(string Prompt, IReadOnlyList<SentenceToken> Tokens);
public sealed record CategorizationPublicContent(IReadOnlyList<CategorizationItem> Items, IReadOnlyList<ExerciseCategory> Categories);
public sealed record SpeakingPublicContent(string Prompt, string ReferenceText, Guid? ReferenceAudioMediaId);
