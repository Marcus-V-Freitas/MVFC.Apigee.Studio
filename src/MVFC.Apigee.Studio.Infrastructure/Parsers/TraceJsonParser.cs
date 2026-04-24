namespace MVFC.Apigee.Studio.Infrastructure.Parsers;

/// <summary>
/// <para>Parses the JSON payload returned by the Apigee Emulator trace endpoint.</para>
/// <para>
/// The emulator returns a JSON with a "Messages" array. Each message contains
/// a "point" array with entries of type: StateChange, FlowInfo, Condition,
/// Execution, Paused, Resumed. Only the relevant types are processed:
/// </para>
/// <para>
///   StateChange  → phase separator (From → To)
///   Condition    → flow/route condition evaluation
///   Execution    → executed policy
/// </para>
/// </summary>
public static class TraceJsonParser
{
    private const string TimestampFormat = "dd-MM-yy HH:mm:ss:fff";

    /// <summary>
    /// Parses the root JSON element returned by GET /v1/emulator/trace/transactions
    /// and returns a list of <see cref="TraceTransaction"/>.
    /// </summary>
    public static IReadOnlyList<TraceTransaction> ParseTransactions(JsonElement root)
    {
        var result = new List<TraceTransaction>();

        if (!root.TryGetProperty("Messages", out var messages))
            return result;

        foreach (var msg in messages.EnumerateArray())
        {
            if (!msg.TryGetProperty("point", out var pointArray))
                continue;

            var (points, verb, uri, statusCode, firstTs, lastTs) = ParsePoints(pointArray);

            var msgId = TryGetString(msg, "messageId");
            if (string.IsNullOrEmpty(msgId))
            {
                // Fallback to stable hash if messageId is missing
                msgId = string.Create(CultureInfo.InvariantCulture, $"{firstTs}_{uri.GetHashCode(StringComparison.Ordinal)}");
            }

            result.Add(new TraceTransaction
            {
                MessageId = msgId,
                RequestMethod = verb,
                RequestUri = uri,
                ResponseCode = int.TryParse(statusCode, CultureInfo.InvariantCulture, out var sc) ? sc : 0,
                TotalTimeMs = lastTs > firstTs ? lastTs - firstTs : 0,
                Points = points,
            });
        }

        return result;
    }

    private static (List<TracePoint> points, string verb, string uri, string statusCode, long firstTs, long lastTs)
    ParsePoints(JsonElement pointArray)
    {
        var points = new List<TracePoint>();
        var verb = string.Empty;
        var uri = string.Empty;
        var statusCode = string.Empty;
        long firstTs = 0;
        long lastTs = 0;
        long prevTs = 0;

        foreach (var point in pointArray.EnumerateArray())
        {
            var pointId = TryGetString(point, "id") ?? string.Empty;

            if (!point.TryGetProperty("results", out var results))
                continue;

            long currentTs = 0;
            ExtractRequestResponseInfo(results, ref verb, ref uri, ref statusCode, ref firstTs, ref lastTs);

            foreach (var res in results.EnumerateArray()
                                       .Where(res => string.Equals(TryGetString(res, "ActionResult"), "DebugInfo", StringComparison.OrdinalIgnoreCase)))
            {
                var ts = TryGetString(res, "timestamp");
                if (ts is not null && TryParseEmulatorTimestamp(ts, out var ms))
                    currentTs = ms;
            }

            if (pointId is not ("StateChange" or "Condition" or "Execution"))
            {
                prevTs = currentTs > 0 ? currentTs : prevTs;
                continue;
            }

            var elapsed = (currentTs > 0 && prevTs > 0) ? currentTs - prevTs : 0;
            prevTs = currentTs > 0 ? currentTs : prevTs;

            points.Add(ParseTracePoint(point, pointId, elapsed));
        }

        return (points, verb, uri, statusCode, firstTs, lastTs);
    }

    private static void ExtractRequestResponseInfo(
        JsonElement results,
        ref string verb,
        ref string uri,
        ref string statusCode,
        ref long firstTs,
        ref long lastTs)
    {
        foreach (var res in results.EnumerateArray())
        {
            var actionResult = TryGetString(res, "ActionResult");

            if (string.Equals(actionResult, "RequestMessage", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(verb))
            {
                verb = TryGetString(res, "verb") ?? string.Empty;
                uri = TryGetString(res, "uRI") ?? string.Empty;
            }

            if (string.Equals(actionResult, "ResponseMessage", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(statusCode))
                statusCode = TryGetString(res, "statusCode") ?? string.Empty;

            if (string.Equals(actionResult, "DebugInfo", StringComparison.OrdinalIgnoreCase))
            {
                var ts = TryGetString(res, "timestamp");
                if (ts is not null && TryParseEmulatorTimestamp(ts, out var ms))
                {
                    if (firstTs == 0) firstTs = ms;
                    lastTs = ms;
                }
            }
        }
    }

    /// <summary>
    /// Parses a single trace point JSON element into a <see cref="TracePoint"/>.
    /// Mapping rules:
    ///   StateChange → PolicyName = "To"; Description = "From → To"
    ///   Condition   → PolicyName = Expression; Description = ExpressionResult
    ///   Execution   → PolicyName = stepDefinition-name; Phase = enforcement direction
    /// </summary>
    public static TracePoint ParseTracePoint(JsonElement point, string pointId, long elapsedMs = 0)
    {
        var variables = ExtractVariables(point);

        string policyName, phase, description;

        switch (pointId)
        {
            case "StateChange":
                var from = variables.GetValueOrDefault("From", string.Empty);
                var to = variables.GetValueOrDefault("To", string.Empty);
                policyName = to;
                phase = to;
                description = string.IsNullOrEmpty(from) ? to : $"{from} → {to}";
                break;

            case "Condition":
                policyName = variables.GetValueOrDefault("Expression", "Condition");
                phase = string.Empty;
                description = variables.GetValueOrDefault("ExpressionResult", string.Empty);
                break;

            default: // Execution
                policyName = variables.GetValueOrDefault("stepDefinition-name",
                              variables.GetValueOrDefault("policy.name", pointId));
                phase = variables.GetValueOrDefault("enforcement",
                              variables.GetValueOrDefault("current.flow.direction", string.Empty));
                description = variables.GetValueOrDefault("type", string.Empty);
                break;
        }

        var hasError = string.Equals(variables.GetValueOrDefault("result"), "false", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(variables.GetValueOrDefault("failed"), "true", StringComparison.OrdinalIgnoreCase);

        return new TracePoint
        {
            PointType = pointId,
            PolicyName = policyName,
            Phase = phase,
            Description = description,
            ElapsedTimeMs = elapsedMs,
            HasError = hasError,
            Variables = variables,
        };
    }

    /// <summary>
    /// Extracts all key-value variables from a point's results array.
    /// Sources: properties.property[], headers[], and body content.
    /// </summary>
    private static Dictionary<string, string> ExtractVariables(JsonElement point)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!point.TryGetProperty("results", out var results))
            return variables;

        foreach (var result in results.EnumerateArray())
        {
            var actionResult = TryGetString(result, "ActionResult");

            if (result.TryGetProperty("properties", out var props)
                && props.TryGetProperty("property", out var propArray))
            {
                foreach (var prop in propArray.EnumerateArray())
                {
                    var name = TryGetString(prop, "name");
                    var value = TryGetString(prop, "value");
                    if (name is not null && value is not null)
                        variables.TryAdd(name, value);
                }
            }

            if (result.TryGetProperty("headers", out var headersArray))
            {
                var prefix = actionResult switch
                {
                    "RequestMessage" => "request.header.",
                    "ResponseMessage" => "response.header.",
                    _ => "message.header.",
                };

                foreach (var header in headersArray.EnumerateArray())
                {
                    var name = TryGetString(header, "name");
                    var value = TryGetString(header, "value");
                    if (name is not null && value is not null)
                        variables.TryAdd($"{prefix}{name}", value);
                }
            }

            if (result.TryGetProperty("content", out var bodyEl)
                && bodyEl.ValueKind == JsonValueKind.String)
            {
                var prefix = actionResult switch
                {
                    "RequestMessage" => "request.",
                    "ResponseMessage" => "response.",
                    _ => "message.",
                };

                variables.TryAdd($"{prefix}content", bodyEl.GetString() ?? string.Empty);
            }
        }

        return variables;
    }

    /// <summary>
    /// Returns the string value of <paramref name="key"/> in <paramref name="el"/>,
    /// or null if absent or not a string.
    /// </summary>
    public static string? TryGetString(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>
    /// Parses a timestamp in the emulator format "dd-MM-yy HH:mm:ss:fff"
    /// into Unix epoch milliseconds (UTC).
    /// </summary>
    public static bool TryParseEmulatorTimestamp(string ts, out long epochMs)
    {
        epochMs = 0;
        if (!DateTime.TryParseExact(ts, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return false;
        }

        epochMs = new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeMilliseconds();
        return true;
    }
}