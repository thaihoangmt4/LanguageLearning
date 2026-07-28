namespace LanguageLearning.Common.Constants;

/// <summary>
/// Application-wide constants.
/// </summary>
public static class AppConstants
{
    public const string ConnectionStringName = "DefaultConnection";

    public static class Policies
    {
        public const string AdminOnly = nameof(AdminOnly);
        public const string UserOnly = nameof(UserOnly);
    }

    public static class Roles
    {
        public const string Admin = nameof(Admin);
        public const string User = nameof(User);
    }
}
