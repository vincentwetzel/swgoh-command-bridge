#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace swgoh_command_bridge.Core.Services;

/// <summary>
/// Retrieves the current game catalog from Comlink and activates it only after
/// the local snapshot service has validated the complete result.
/// </summary>
public sealed class ComlinkCatalogRefreshService
{
    private readonly ICharacterCatalogService _comlinkCatalogService;
    private readonly ICharacterCatalogSnapshotService _snapshotService;

    public ComlinkCatalogRefreshService(
        ICharacterCatalogService comlinkCatalogService,
        ICharacterCatalogSnapshotService snapshotService)
    {
        ArgumentNullException.ThrowIfNull(comlinkCatalogService);
        ArgumentNullException.ThrowIfNull(snapshotService);

        _comlinkCatalogService = comlinkCatalogService;
        _snapshotService = snapshotService;
    }

    public async Task<CharacterCatalogSnapshotInfo> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        var payload = await _comlinkCatalogService
            .FetchCharacterCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        return await _snapshotService
            .UpdateFromCatalogAsync(payload, cancellationToken)
            .ConfigureAwait(false);
    }
}
