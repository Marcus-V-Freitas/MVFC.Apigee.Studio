namespace MVFC.Apigee.Studio.Domain.Entities;

public sealed class ApigeeLintMessage(int line, int column, string message, int severity)
{
    public int Line { get; set; } = line;
    public int Column { get; set; } = column;
    public string Message { get; set; } = message;
    public int Severity { get; set; } = severity; // 1 = Warning, 2 = Error
}
