namespace ApigeeLocalDev.Domain.Entities;

/// <summary>
/// Representa uma sessão de debug ativa no Apigee Emulator.
/// </summary>
public record TraceSession(
    string SessionId,
    string ApiProxy,
    string Revision,
    string Environment,
    DateTime CreatedAt);
