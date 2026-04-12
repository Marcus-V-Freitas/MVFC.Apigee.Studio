namespace ApigeeLocalDev.Web.Domain.Models;

public sealed class TextFile
{
    public string RelativePath { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
