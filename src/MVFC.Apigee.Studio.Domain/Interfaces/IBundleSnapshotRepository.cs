namespace MVFC.Apigee.Studio.Domain.Interfaces;

/// <summary>
/// Manages snapshots of the workspace bundle for diffing purposes.
/// </summary>
public interface IBundleSnapshotRepository
{
    /// <summary>
    /// Captures a new snapshot of the workspace.
    /// </summary>
    Task CreateSnapshotAsync(ApigeeWorkspace workspace, CancellationToken ct = default);

    /// <summary>
    /// Compares the current workspace state with the last captured snapshot.
    /// </summary>
    Task<BundleDiff> GetDiffAsync(ApigeeWorkspace workspace, CancellationToken ct = default);
}
