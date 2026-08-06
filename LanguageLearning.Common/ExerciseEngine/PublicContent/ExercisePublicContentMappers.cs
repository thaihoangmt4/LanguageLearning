using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.ExerciseEngine.Validation;
using LanguageLearning.Common.Results;

namespace LanguageLearning.Common.ExerciseEngine.PublicContent;

public abstract class PublicContentMappingStrategy<TContent> : IExercisePublicContentMappingStrategy
{
    public abstract ExerciseType ExerciseType { get; }
    public abstract object Map(TContent content);
    object IExercisePublicContentMappingStrategy.Map(object content) => Map((TContent)content);
}

public sealed class MultipleChoicePublicMapper : PublicContentMappingStrategy<MultipleChoiceContent>
{
    public override ExerciseType ExerciseType => ExerciseType.MultipleChoice;
    public override object Map(MultipleChoiceContent c) => new MultipleChoicePublicContent(c.Question, c.Options);
}
public sealed class ImageMatchingPublicMapper : PublicContentMappingStrategy<ImageMatchingContent>
{
    public override ExerciseType ExerciseType => ExerciseType.ImageMatching;
    public override object Map(ImageMatchingContent c) => new ImageMatchingPublicContent(c.Sources, c.Targets);
}
public sealed class AudioMatchingPublicMapper : PublicContentMappingStrategy<AudioMatchingContent>
{
    public override ExerciseType ExerciseType => ExerciseType.AudioMatching;
    public override object Map(AudioMatchingContent c) => new AudioMatchingPublicContent(c.PronunciationText, c.Options);
}
public sealed class TypingPublicMapper : PublicContentMappingStrategy<TypingContent>
{
    public override ExerciseType ExerciseType => ExerciseType.Typing;
    public override object Map(TypingContent c) => new TypingPublicContent(c.Prompt, c.MaxLength);
}
public sealed class SentenceOrderingPublicMapper : PublicContentMappingStrategy<SentenceOrderingContent>
{
    public override ExerciseType ExerciseType => ExerciseType.SentenceOrdering;
    public override object Map(SentenceOrderingContent c) => new SentenceOrderingPublicContent(c.Prompt, c.Tokens);
}
public sealed class CategorizationPublicMapper : PublicContentMappingStrategy<CategorizationContent>
{
    public override ExerciseType ExerciseType => ExerciseType.Categorization;
    public override object Map(CategorizationContent c) => new CategorizationPublicContent(c.Items, c.Categories);
}
public sealed class SpeakingPublicMapper : PublicContentMappingStrategy<SpeakingContent>
{
    public override ExerciseType ExerciseType => ExerciseType.Speaking;
    public override object Map(SpeakingContent c) => new SpeakingPublicContent(c.Prompt, c.ReferenceText, c.ReferenceAudioMediaId);
}

public sealed class ExercisePublicContentMapper : IExercisePublicContentMapper
{
    private readonly IReadOnlyDictionary<ExerciseType, IExercisePublicContentMappingStrategy> _mappers;
    public ExercisePublicContentMapper(IEnumerable<IExercisePublicContentMappingStrategy> mappers) =>
        _mappers = Registry.Build(mappers, x => x.ExerciseType, "public mapper");
    public Result<object> Map(ExerciseType type, object content) => _mappers.TryGetValue(type, out var mapper)
        ? Result<object>.Success(mapper.Map(content))
        : Result<object>.Failure(ExerciseEngineErrors.PublicMapperNotRegistered);
}
