namespace ApigeeLocalDev.Domain.Entities;

/// <summary>
/// Representa uma sessão de trace iniciada no emulator.
/// Retornada por POST /v1/emulator/trace?proxyName={proxy}
/// </summary>
public sealed class TraceSession
{
    public string SessionId    { get; init; } = string.Empty;
    public string ApiProxy     { get; init; } = string.Empty;
    public string Application  { get; init; } = string.Empty;
    public string Organization { get; init; } = string.Empty;
    public string Environment  { get; init; } = string.Empty;
    public string Revision     { get; init; } = string.Empty;
    public DateTime StartedAt  { get; init; } = DateTime.UtcNow;
}
