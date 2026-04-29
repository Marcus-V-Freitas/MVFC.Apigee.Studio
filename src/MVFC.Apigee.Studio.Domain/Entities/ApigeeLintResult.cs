namespace MVFC.Apigee.Studio.Domain.Entities;

public sealed class ApigeeLintResult(string filePath, IList<ApigeeLintMessage> messages)
{
    public string FilePath { get; set; } = filePath;
    public IList<ApigeeLintMessage> Messages { get; set; } = messages;
}
