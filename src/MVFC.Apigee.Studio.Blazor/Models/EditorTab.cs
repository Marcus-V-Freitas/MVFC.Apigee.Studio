namespace MVFC.Apigee.Studio.Blazor.Models;

public sealed class EditorTab
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FullPath);
    public string Content { get; set; } = string.Empty;
    public bool IsDirty { get; set; }
}
