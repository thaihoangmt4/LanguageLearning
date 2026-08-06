namespace LanguageLearning.WebApi.Services;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
}
