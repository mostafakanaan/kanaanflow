namespace KanaanFlow.Core.Abstractions;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface ISyncService
{
    Task SyncAsync(CancellationToken cancellationToken);
    Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken);
}
