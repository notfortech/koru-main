using System.Threading;

namespace StudioTechBI.API.Services;

/// <summary>
/// Set when <see cref="StartupDbTasksHostedService"/> has finished migrations/seed.
/// API requests to /api/* are rejected until this is true, so we never return 500 from half-ready DBs.
/// </summary>
public sealed class DatabaseReadinessState
{
    private int _ready;

    public bool IsDatabaseReady => Volatile.Read(ref _ready) == 1;

    public void MarkDatabaseReady() => Volatile.Write(ref _ready, 1);
}
