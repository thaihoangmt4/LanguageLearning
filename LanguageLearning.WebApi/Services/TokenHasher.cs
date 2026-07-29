using System.Security.Cryptography;
using System.Text;

namespace LanguageLearning.WebApi.Services;

/// <summary>
/// SHA-256 based token hasher.
/// </summary>
public sealed class TokenHasher : ITokenHasher
{
    public string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
