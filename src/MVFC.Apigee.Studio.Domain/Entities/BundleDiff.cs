namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents the difference between the current workspace state and the last deployed snapshot.
/// </summary>
public sealed record BundleDiff(
    IReadOnlyList<string> AddedFiles,
    IReadOnlyList<string> ModifiedFiles,
    IReadOnlyList<string> RemovedFiles
)
{
    public bool HasChanges => AddedFiles.Count > 0 || ModifiedFiles.Count > 0 || RemovedFiles.Count > 0;
}
