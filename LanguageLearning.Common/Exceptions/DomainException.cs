namespace LanguageLearning.Common.Exceptions;

/// <summary>
/// Base exception for domain-level errors.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a requested entity is not found.
/// </summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.")
    {
    }
}

/// <summary>
/// Exception thrown when a business rule is violated.
/// </summary>
public sealed class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message)
    {
    }
}

/// <summary>
/// Exception thrown when an operation is not authorized.
/// </summary>
public sealed class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message = "The current user is not authorized to perform this operation.")
        : base(message)
    {
    }
}
