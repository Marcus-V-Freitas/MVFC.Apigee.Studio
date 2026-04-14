using System.Text.Json.Serialization;

namespace ApigeeLocalDev.Infrastructure.Http.Models;

// ─── Raiz ────────────────────────────────────────────────────────────────────

/// <summary>
/// Mapeia exatamente o JSON retornado pelo emulator em:
///   GET /v1/organizations/emulator/environments/{env}/apis/{proxy}/revisions/{rev}/debugsessions/{sessionId}/data
///
/// Estrutura real observada:
/// {
///   "DebugSession": { "SessionId": "...", ... },
///   "Messages": [
///     { "completed": true, "point": [ { "id": "StateChange", "results": [...] }, ... ] },
///     ...
///   ]
/// }
/// </summary>
public sealed class DebugSessionResponse
{
    [JsonPropertyName("DebugSession")]
    public DebugSessionInfo Session { get; init; } = new();

    [JsonPropertyName("Messages")]
    public List<DebugMessage> Messages { get; init; } = [];
}

public sealed class DebugSessionInfo
{
    [JsonPropertyName("Organization")]
    public string Organization { get; init; } = string.Empty;

    [JsonPropertyName("Environment")]
    public string Environment { get; init; } = string.Empty;

    [JsonPropertyName("Revision")]
    public string Revision { get; init; } = string.Empty;

    [JsonPropertyName("SessionId")]
    public string SessionId { get; init; } = string.Empty;
}

// ─── Mensagem (transação) ─────────────────────────────────────────────────────

public sealed class DebugMessage
{
    [JsonPropertyName("completed")]
    public bool Completed { get; init; }

    /// <summary>Array de pontos de execução do flow.</summary>
    [JsonPropertyName("point")]
    public List<DebugPoint> Points { get; init; } = [];
}

// ─── Ponto ────────────────────────────────────────────────────────────────────

public sealed class DebugPoint
{
    /// <summary>StateChange | Execution | Condition | FlowInfo | Paused | Resumed | DebugMask</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("results")]
    public List<DebugResult> Results { get; init; } = [];
}

// ─── Result ───────────────────────────────────────────────────────────────────

public sealed class DebugResult
{
    /// <summary>DebugInfo | RequestMessage | ResponseMessage | VariableAccess</summary>
    [JsonPropertyName("ActionResult")]
    public string ActionResult { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    // DebugInfo
    [JsonPropertyName("properties")]
    public DebugProperties? Properties { get; init; }

    // RequestMessage
    [JsonPropertyName("verb")]
    public string? Verb { get; init; }

    [JsonPropertyName("uRI")]
    public string? Uri { get; init; }

    // ResponseMessage
    [JsonPropertyName("statusCode")]
    public string? StatusCode { get; init; }

    [JsonPropertyName("reasonPhrase")]
    public string? ReasonPhrase { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("headers")]
    public List<DebugHeader>? Headers { get; init; }
}

// ─── Properties ───────────────────────────────────────────────────────────────

public sealed class DebugProperties
{
    [JsonPropertyName("property")]
    public List<DebugProperty> Property { get; init; } = [];
}

public sealed class DebugProperty
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;
}

public sealed class DebugHeader
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;
}
