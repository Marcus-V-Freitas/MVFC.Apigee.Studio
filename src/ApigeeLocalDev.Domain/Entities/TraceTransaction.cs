namespace ApigeeLocalDev.Domain.Entities;

/// <summary>
/// Representa uma requisição capturada durante uma sessão de trace,
/// contendo todos os pontos de execução de políticas.
/// </summary>
public record TraceTransaction(
    string MessageId,
    string RequestPath,
    string Verb,
    int StatusCode,
    List<TracePoint> Points);
