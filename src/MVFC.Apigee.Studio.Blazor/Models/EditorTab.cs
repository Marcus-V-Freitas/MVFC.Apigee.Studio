namespace MVFC.Apigee.Studio.Blazor.Models;

/// <summary>
/// Represents an open editor tab, including its file path, content, and dirty state.
/// </summary>
public sealed class EditorTab
{
    /// <summary>
    /// The full file path of the file opened in the tab.
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// The file name extracted from the full path.
    /// </summary>
    public string FileName => Path.GetFileName(FullPath);

    /// <summary>
    /// The current content of the file in the editor.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the content has unsaved changes.
    /// </summary>
    public bool IsDirty { get; set; }
}
