namespace ApigeeLocalDev.Domain.Entities;

/// <summary>
/// Representa uma requisição interceptada pelo TraceMiddleware,
/// capturada antes de ser repassada ao emulator runtime (:8998).
/// </summary>
public record TraceTransaction(
    string MessageId,
    string RequestPath,
    string Verb,
    int StatusCode,
    long DurationMs,
    string? RequestBody,
    string? ResponseBody,
    List<TracePoint> Points);
