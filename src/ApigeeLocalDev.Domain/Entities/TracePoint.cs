namespace ApigeeLocalDev.Domain.Entities;

/// <summary>
/// Representa a execução de uma política individual dentro de uma transação de trace.
/// </summary>
public record TracePoint(
    string Policy,
    string Phase,
    bool Executed,
    bool Error,
    long DurationMs,
    Dictionary<string, string> Variables);
