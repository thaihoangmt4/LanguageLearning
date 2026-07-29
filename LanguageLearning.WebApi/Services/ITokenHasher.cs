namespace LanguageLearning.WebApi.Services;

/// <summary>
/// Hashes sensitive token values so only the hash is persisted.
/// </summary>
public interface ITokenHasher
{
    /// <summary>
    /// Produces a one-way hash of the given token.
    /// </summary>
    string Hash(string token);
}
