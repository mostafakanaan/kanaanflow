namespace KanaanFlow.Sync;

using KanaanFlow.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class SyncService : ISyncService
{
    private readonly ILogger<SyncService> logger;

    public SyncService(ILogger<SyncService> loggerInstance)
    {
        logger = loggerInstance;
    }

    public Task SyncAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("sync not yet implemented");
        return Task.CompletedTask;
    }

    public Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("sync not yet implemented");
        return Task.FromResult<DateTime?>(null);
    }
}
