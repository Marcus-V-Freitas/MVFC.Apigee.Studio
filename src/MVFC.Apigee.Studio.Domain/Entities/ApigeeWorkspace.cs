namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents an Apigee workspace, including its name and root path.
/// </summary>
/// <param name="Name">The name of the workspace.</param>
/// <param name="RootPath">The root directory path of the workspace.</param>
public sealed record ApigeeWorkspace(
    string Name,
    string RootPath);