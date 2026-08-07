using System.Security.Cryptography;
using System.Text;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.WebApi.Features.ExerciseGeneration;

public static class ExerciseContentHasher
{
    public static string Compute(ExerciseType type, string question)
    {
        var normalized = Normalize(question);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{type}:{normalized}"));
        return Convert.ToHexString(bytes);
    }

    internal static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSpace && builder.Length > 0) builder.Append(' ');
                builder.Append(character);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }
}
