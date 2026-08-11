namespace LanguageLearning.WebApi.Features.System.DatabaseMigration;

public sealed class DatabaseMigrationGuard
{
    private int _isRunning;

    public bool TryAcquire() => Interlocked.CompareExchange(ref _isRunning, 1, 0) == 0;

    public void Release() => Volatile.Write(ref _isRunning, 0);
}
