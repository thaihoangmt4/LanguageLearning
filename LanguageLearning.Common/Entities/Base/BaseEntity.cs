namespace LanguageLearning.Common.Entities.Base;

/// <summary>
/// Base entity class providing a common identifier for all domain entities.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
