namespace ApigeeLocalDev.Domain.Entities;

/// <summary>
/// Uma transação capturada dentro de uma sessão de trace.
///
/// Mapeada de DebugSessionResponse.Messages[] retornado pelo emulator.
/// O emulator NÃO expõe messageId individual — o índice do array (tx-0, tx-1, ...)
/// é usado como identificador único.
/// </summary>
public sealed record TraceTransaction(
    string MessageId,
    string RequestPath,
    string Verb,
    int    StatusCode,
    long   DurationMs,
    string? RequestBody,
    string? ResponseBody,
    IReadOnlyList<TracePoint> Points);

/// <summary>
/// Um ponto de execução dentro de uma transação.
///
/// Originado de DebugMessage.point[] onde:
///   - id == "Execution"   → política executada (Policy preenchido)
///   - id == "StateChange" → transição de fase do flow (Phase preenchido, Policy vazio)
/// </summary>
public sealed record TracePoint(
    string  Policy,
    string  Phase,
    bool    Executed,
    bool    Error,
    long    DurationMs,
    string? Condition,
    IReadOnlyDictionary<string, string> Variables);
