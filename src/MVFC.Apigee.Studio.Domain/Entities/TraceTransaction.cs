namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Uma transação capturada dentro de uma sessão de trace.
/// Mapeada de DebugSession.Messages[] retornado pelo emulator.
/// </summary>
public sealed class TraceTransaction
{
    public string MessageId     { get; init; } = string.Empty;
    public string RequestMethod { get; init; } = string.Empty;
    public string RequestUri    { get; init; } = string.Empty;
    public int    ResponseCode  { get; init; }
    public long   TotalTimeMs   { get; init; }

    public IReadOnlyList<TracePoint> Points { get; init; } = [];
}
