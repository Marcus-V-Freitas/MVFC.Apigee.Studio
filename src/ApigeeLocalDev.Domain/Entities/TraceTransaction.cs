namespace ApigeeLocalDev.Domain.Entities;

/// <summary>
/// Uma transação capturada dentro de uma sessão de trace.
/// Instanciada pelo TraceMiddleware via sintaxe de positional record.
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
