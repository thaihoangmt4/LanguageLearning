using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.Settings;

public sealed class SystemSettings : BaseEntity
{
    public static readonly Guid SingletonId =
        Guid.Parse("389cd8b7-6f49-4c8f-bdf8-7bcae005b3cc");

    public SystemSettings()
    {
        Id = SingletonId;
    }

    public SystemLogLevel MinimumLogLevel { get; private set; } = SystemLogLevel.Information;
    public bool ExerciseGenerationEnabled { get; private set; } = true;
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public User? UpdatedByUser { get; private set; }

    public void Update(
        SystemLogLevel minimumLogLevel,
        DateTime updatedAtUtc,
        Guid updatedByUserId)
    {
        MinimumLogLevel = minimumLogLevel;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    public void SetExerciseGenerationEnabled(
        bool enabled,
        DateTime updatedAtUtc,
        Guid updatedByUserId)
    {
        ExerciseGenerationEnabled = enabled;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }
}
