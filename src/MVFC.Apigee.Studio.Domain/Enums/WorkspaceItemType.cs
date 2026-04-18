namespace MVFC.Apigee.Studio.Domain.Enums;

/// <summary>
/// Represents the types of items that can exist within a workspace.
/// </summary>
public enum WorkspaceItemType
{
    /// <summary>
    /// An API Proxy item, typically representing an API implementation or gateway configuration.
    /// </summary>
    ApiProxy,

    /// <summary>
    /// A Shared Flow item, used for reusable logic or policies across multiple APIs.
    /// </summary>
    SharedFlow,

    /// <summary>
    /// An Environment item, representing a deployment or runtime environment.
    /// </summary>
    Environment,

    /// <summary>
    /// A File item, representing a single file within the workspace.
    /// </summary>
    File,

    /// <summary>
    /// A Directory item, representing a folder or directory within the workspace.
    /// </summary>
    Directory,
}