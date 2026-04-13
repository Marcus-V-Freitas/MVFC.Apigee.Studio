namespace ApigeeLocalDev.Domain.Entities;

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

/// <summary>
/// Um ponto de execução dentro de uma transação (política, state-change, condition, etc.).
/// </summary>
public sealed class TracePoint
{
    public string PointType     { get; init; } = string.Empty;  // StateChange | Execution | Condition | Error | FlowInfo
    public string PolicyName    { get; init; } = string.Empty;
    public string Phase         { get; init; } = string.Empty;  // ProxyRequestFlow | TargetRequestFlow | etc.
    public string Description   { get; init; } = string.Empty;
    public long   ElapsedTimeMs { get; init; }
    public bool   HasError      { get; init; }
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();
}
