namespace ApigeeLocalDev.Domain.Entities;

/// <summary>
/// Representa a execução (ou definição) de uma política individual
/// dentro de uma transação de trace.
///
/// Quando capturado via proxy reverso (sem Debug API), Executed é inferido:
///   - ProxyRequest steps: sempre true se StatusCode &lt; 500
///   - ProxyResponse steps: sempre true se StatusCode &lt; 500
///   - Error flow steps: true se StatusCode >= 400
/// </summary>
public record TracePoint(
    string Policy,
    string Phase,
    bool Executed,
    bool Error,
    long DurationMs,
    string? Condition,
    Dictionary<string, string> Variables);
