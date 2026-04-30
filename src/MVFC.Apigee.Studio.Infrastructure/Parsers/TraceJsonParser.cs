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
            var application = TryGetString(msg, "application");
            if (string.IsNullOrEmpty(msgId))
            {
                // Fallback to stable hash if messageId is missing
                msgId = string.Create(CultureInfo.InvariantCulture, $"{firstTs}_{uri.GetHashCode(StringComparison.Ordinal)}");
            }

            result.Add(new TraceTransaction
            {
                MessageId = msgId,
                Application = application ?? string.Empty,
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
        string verb = "", uri = "", statusCode = "";
        long firstTs = 0, lastTs = 0, prevTs = 0;
        var flowBuffer = new List<(JsonElement El, string Id, long Elapsed)>();

        foreach (var point in pointArray.EnumerateArray())
        {
            var pointId = TryGetString(point, "id") ?? string.Empty;
            if (!point.TryGetProperty("results", out var results)) continue;

            ExtractRequestResponseInfo(results, ref verb, ref uri, ref statusCode, ref firstTs, ref lastTs);
            var currentTs = ExtractTimestamp(results);
            var elapsed = (currentTs > 0 && prevTs > 0) ? currentTs - prevTs : 0;
            prevTs = currentTs > 0 ? currentTs : prevTs;

            if (string.Equals(pointId, "FlowInfo", StringComparison.Ordinal))
            {
                var quickProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (point.TryGetProperty("results", out var resArray))
                {
                    foreach (var r in resArray.EnumerateArray()) ExtractProperties(r, quickProps);
                }

                // Intelligent skip: ignore FlowInfo if neither proxy nor target flow names are defined
                if (!quickProps.ContainsKey("proxy.flow.name") && !quickProps.ContainsKey("target.flow.name"))
                {
                    continue;
                }

                flowBuffer.Add((point, pointId, elapsed));
                continue;
            }

            if (flowBuffer.Count > 0) { points.Add(AggregateFlowPoints(flowBuffer)); flowBuffer.Clear(); }

            if (pointId is "StateChange" or "Execution" or "FlowCallout" or "FlowReturn")
            {
                var parsedPoint = ParseTracePoint(point, pointId, elapsed);

                // Skip FlowHookAction points as they are internal engine hooks
                if (!string.Equals(parsedPoint.StepType, "FlowHookAction", StringComparison.OrdinalIgnoreCase))
                {
                    points.Add(parsedPoint);
                }
            }
        }

        if (flowBuffer.Count > 0) points.Add(AggregateFlowPoints(flowBuffer));
        return (points, verb, uri, statusCode, firstTs, lastTs);
    }

    private static long ExtractTimestamp(JsonElement results)
    {
        foreach (var res in results.EnumerateArray().Where(res => string.Equals(TryGetString(res, "ActionResult"), "DebugInfo", StringComparison.OrdinalIgnoreCase)))
        {
            var ts = TryGetString(res, "timestamp");
            if (ts is not null && TryParseEmulatorTimestamp(ts, out var ms)) return ms;
        }
        return 0;
    }

    private static TracePoint AggregateFlowPoints(List<(JsonElement El, string Id, long Elapsed)> buffer)
    {
        var mergedProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mergedVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mergedRead = new List<(string Name, string Value)>();
        var mergedSet = new List<(string Name, string Value, bool Success)>();
        var mergedRemoved = new List<(string Name, bool Success)>();
        var mergedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? msgContent = null;
        string? reqVerb = null;
        string? reqUri = null;
        long totalElapsed = 0;

        foreach (var item in buffer)
        {
            totalElapsed += item.Elapsed;
            var data = ExtractData(item.El);
            foreach (var p in data.Properties) mergedProps.TryAdd(p.Key, p.Value);
            foreach (var v in data.Variables) mergedVars.TryAdd(v.Key, v.Value);
            foreach (var h in data.MessageHeaders) mergedHeaders.TryAdd(h.Key, h.Value);
            mergedRead.AddRange(data.Read);
            mergedSet.AddRange(data.Set);
            mergedRemoved.AddRange(data.Removed);
            msgContent ??= data.MessageContent;
            reqVerb ??= data.RequestVerb;
            reqUri ??= data.RequestUri;
        }

        var template = buffer[0];
        var mergedData = new ExtractedData(mergedProps, mergedVars, mergedRead, mergedSet, mergedRemoved, mergedHeaders, msgContent, reqVerb, reqUri);
        return ParseTracePointInternal(template.Id, totalElapsed, mergedData);
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

            // Extract Verb and URI from RequestMessage
            if (string.Equals(actionResult, "RequestMessage", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(verb))
            {
                verb = TryGetString(res, "verb") ?? string.Empty;
                uri = TryGetString(res, "uRI") ?? string.Empty;
            }

            // Extract StatusCode from ResponseMessage or ResponseMessageSent
            if ((string.Equals(actionResult, "ResponseMessage", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(actionResult, "ResponseMessageSent", StringComparison.OrdinalIgnoreCase))
                && string.IsNullOrEmpty(statusCode))
            {
                var sc = TryGetString(res, "statusCode");
                if (!string.IsNullOrEmpty(sc)) statusCode = sc;
            }

            // Fallback: Extract from variables/properties if still empty
            if (string.IsNullOrEmpty(statusCode) && res.TryGetProperty("properties", out var props) && props.TryGetProperty("property", out var propArray))
            {
                foreach (var prop in propArray.EnumerateArray())
                {
                    var name = TryGetString(prop, "name");
                    if (name is "response.status.code" or "error.status.code" or "message.status.code")
                    {
                        var val = TryGetString(prop, "value");
                        if (!string.IsNullOrEmpty(val) && !string.Equals(val, "0", StringComparison.Ordinal)) statusCode = val;
                    }
                }
            }

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
        var data = ExtractData(point);
        return ParseTracePointInternal(pointId, elapsedMs, data);
    }

    private static TracePoint ParseTracePointInternal(
        string pointId,
        long elapsedMs,
        ExtractedData data)
    {
        var (policyName, phase, description, stepType) = MapPointProperties(pointId, data.Properties);

        // Enhance description for Execution points
        if (string.Equals(pointId, "Execution", StringComparison.Ordinal))
        {
            var exprResult = data.Properties.GetValueOrDefault("ExpressionResult");
            var skipped = string.Equals(exprResult, "false", StringComparison.OrdinalIgnoreCase) ? " (skipped)" : "";
            if (data.Properties.TryGetValue("stepDefinition-name", out var stepDefName))
                description = $"{stepDefName} ({stepType}){skipped}";
            else
                description = $"{stepType}{skipped}";
        }
        else if (string.Equals(pointId, "FlowCallout", StringComparison.Ordinal))
        {
            var sharedFlow = data.Properties.GetValueOrDefault("shared.flow.name");
            description = $"Start {sharedFlow}";
        }
        else if (string.Equals(pointId, "FlowReturn", StringComparison.Ordinal))
        {
            var sharedFlow = data.Properties.GetValueOrDefault("shared.flow.name");
            description = $"End {sharedFlow}";
        }

        var errorMessage = data.Variables.GetValueOrDefault("error") ?? data.Variables.GetValueOrDefault("error.message");
        var errorCode = data.Variables.GetValueOrDefault("error.code") ?? data.Variables.GetValueOrDefault("fault.name");

        var hasError = !string.IsNullOrEmpty(errorMessage) ||
                       string.Equals(data.Variables.GetValueOrDefault("result"), "false", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(data.Variables.GetValueOrDefault("failed"), "true", StringComparison.OrdinalIgnoreCase);

        return new TracePoint
        {
            PointType = MapFriendlyPointType(pointId),
            RawPointId = pointId,
            PolicyName = policyName,
            Phase = phase,
            StepType = stepType,
            Description = description,
            ElapsedTimeMs = elapsedMs,
            HasError = hasError,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            Properties = data.Properties,
            Variables = data.Variables,
            VariablesRead = data.Read,
            VariablesSet = data.Set,
            VariablesRemoved = data.Removed,
            MessageHeaders = data.MessageHeaders,
            MessageContent = data.MessageContent,
            RequestVerb = data.RequestVerb,
            RequestUri = data.RequestUri
        };
    }

    private static string MapFriendlyPointType(string pointId) => pointId switch
    {
        "Execution" => "Step",
        "StateChange" => "State",
        "FlowInfo" => "Flow",
        _ => pointId
    };

    private static (string Name, string Phase, string Desc, string StepType) MapPointProperties(string pointId, Dictionary<string, string> variables)
    {
        return pointId switch
        {
            "StateChange" => ParseStateChange(variables),
            "Condition" => (variables.GetValueOrDefault("Expression", "Condition"), string.Empty, variables.GetValueOrDefault("ExpressionResult", string.Empty), "Condition"),
            "FlowInfo" => ParseFlowInfo(variables),
            _ => ParseExecution(pointId, variables)
        };
    }

    private static (string Name, string Phase, string Desc, string StepType) ParseFlowInfo(Dictionary<string, string> variables)
    {
        var flowName = variables.GetValueOrDefault("proxy.flow.name")
                    ?? variables.GetValueOrDefault("target.flow.name")
                    ?? variables.GetValueOrDefault("flow.name")
                    ?? variables.GetValueOrDefault("flow.type", "Flow transition");

        var direction = variables.GetValueOrDefault("current.flow.direction", string.Empty);
        var type = variables.GetValueOrDefault("flow.type", string.Empty);

        return (flowName, direction, type, "Flow");
    }

    private static (string Name, string Phase, string Desc, string StepType) ParseStateChange(Dictionary<string, string> variables)
    {
        var from = variables.GetValueOrDefault("From", string.Empty);
        var to = variables.GetValueOrDefault("To", string.Empty);
        return (to, to, string.IsNullOrEmpty(from) ? to : $"{from} → {to}", "StateChange");
    }

    private static (string Name, string Phase, string Desc, string StepType) ParseExecution(string pointId, Dictionary<string, string> variables)
    {
        var policyName = variables.GetValueOrDefault("stepDefinition-name",
                           variables.GetValueOrDefault("policy.name", pointId));
        var phase = variables.GetValueOrDefault("enforcement",
                       variables.GetValueOrDefault("current.flow.direction", string.Empty));
        var description = variables.GetValueOrDefault("type", string.Empty);
        var stepType = variables.GetValueOrDefault("type", "Policy");
        return (policyName, phase, description, stepType);
    }

    private sealed record ExtractedData(
        Dictionary<string, string> Properties,
        Dictionary<string, string> Variables,
        List<(string Name, string Value)> Read,
        List<(string Name, string Value, bool Success)> Set,
        List<(string Name, bool Success)> Removed,
        Dictionary<string, string> MessageHeaders,
        string? MessageContent,
        string? RequestVerb,
        string? RequestUri);

    private static ExtractedData ExtractData(JsonElement point)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var read = new List<(string Name, string Value)>();
        var set = new List<(string Name, string Value, bool Success)>();
        var removed = new List<(string Name, bool Success)>();

        var messageHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? messageContent = null;
        string? requestVerb = null;
        string? requestUri = null;

        if (!point.TryGetProperty("results", out var results))
            return new ExtractedData(properties, variables, read, set, removed, messageHeaders, messageContent, requestVerb, requestUri);

        foreach (var result in results.EnumerateArray())
        {
            var actionResult = TryGetString(result, "ActionResult");

            if (string.Equals(actionResult, "VariableAccess", StringComparison.OrdinalIgnoreCase))
            {
                ExtractVariableAccess(result, read, set, removed);
            }
            else if (string.Equals(actionResult, "RequestMessage", StringComparison.OrdinalIgnoreCase))
            {
                requestVerb = TryGetString(result, "verb");
                requestUri = TryGetString(result, "uRI");
                messageContent = TryGetString(result, "content");
                ExtractMessageHeaders(result, messageHeaders);
            }
            else if (string.Equals(actionResult, "ResponseMessage", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(actionResult, "ErrorMessage", StringComparison.OrdinalIgnoreCase))
            {
                messageContent = TryGetString(result, "content");
                ExtractMessageHeaders(result, messageHeaders);
            }

            ExtractProperties(result, properties, variables);
            ExtractVariableArray(result, variables);
            ExtractErrors(result, variables);
            ExtractHeaders(result, actionResult, variables);
            ExtractContent(result, actionResult, variables);

        }

        return new ExtractedData(properties, variables, read, set, removed, messageHeaders, messageContent, requestVerb, requestUri);
    }

    private static void ExtractMessageHeaders(JsonElement result, Dictionary<string, string> headers)
    {
        if (!result.TryGetProperty("headers", out var headersArray)) return;
        foreach (var header in headersArray.EnumerateArray())
        {
            var name = TryGetString(header, "name");
            var value = TryGetString(header, "value");
            if (name is not null && value is not null) headers[name] = value;
        }
    }

    // Known Apigee Emulator technical fields that belong to Properties, not Variables.
    private static readonly HashSet<string> TechnicalPropertyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "action", "enforcement", "internal", "result", "failed",
        "stepDefinition-async", "stepDefinition-continueOnError", "stepDefinition-displayName",
        "stepDefinition-enabled", "stepDefinition-name", "stepDefinition-type",
        "type", "From", "To", "Expression", "ExpressionResult",
        "flow.name", "flow.type", "flow.state", "current.flow.direction",
    };

    private static void ExtractProperties(JsonElement result, Dictionary<string, string> properties, Dictionary<string, string>? variables = null)
    {
        if (!result.TryGetProperty("properties", out var props) || !props.TryGetProperty("property", out var propArray))
            return;

        foreach (var prop in propArray.EnumerateArray())
        {
            var name = TryGetString(prop, "name");
            var value = TryGetString(prop, "value");
            if (name is null || value is null) continue;

            // Route: technical keys → Properties; everything else → Variables
            if (TechnicalPropertyKeys.Contains(name) || name.StartsWith("stepDefinition-", StringComparison.OrdinalIgnoreCase))
                properties.TryAdd(name, value);
            else
                variables?.TryAdd(name, value);
        }
    }

    private static void ExtractVariableAccess(
        JsonElement result,
        List<(string Name, string Value)> read,
        List<(string Name, string Value, bool Success)> set,
        List<(string Name, bool Success)> removed)
    {
        if (!result.TryGetProperty("accessList", out var accessList))
            return;

        foreach (var access in accessList.EnumerateArray())
        {
            if (access.TryGetProperty("Get", out var getProp))
            {
                var name = TryGetString(getProp, "name");
                var value = TryGetString(getProp, "value") ?? string.Empty;
                if (name is not null) read.Add((name, value));
            }
            if (access.TryGetProperty("Set", out var setProp))
            {
                var name = TryGetString(setProp, "name");
                var value = TryGetString(setProp, "value") ?? string.Empty;
                var success = setProp.TryGetProperty("success", out var succProp) && succProp.GetBoolean();
                if (name is not null) set.Add((name, value, success));
            }
            if (access.TryGetProperty("Remove", out var removeProp))
            {
                var name = TryGetString(removeProp, "name");
                var success = removeProp.TryGetProperty("success", out var succProp) && succProp.GetBoolean();
                if (name is not null) removed.Add((name, success));
            }
        }
    }

    private static void ExtractVariableArray(JsonElement result, Dictionary<string, string> variables)
    {
        // Try 'variable', 'variables', or 'contextVariables'
        string[] candidates = { "variable", "variables", "contextVariables" };
        foreach (var key in candidates)
        {
            if (!result.TryGetProperty(key, out var prop)) continue;

            if (prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in prop.EnumerateArray())
                {
                    var name = TryGetString(v, "name") ?? TryGetString(v, "key");
                    var value = TryGetString(v, "value");
                    if (name is not null && value is not null) variables.TryAdd(name, value);
                }
            }
            else if (prop.ValueKind == JsonValueKind.Object)
            {
                foreach (var v in prop.EnumerateObject())
                {
                    variables.TryAdd(v.Name, v.Value.ToString());
                }
            }
        }
    }

    private static void ExtractErrors(JsonElement result, Dictionary<string, string> variables)
    {
        if (result.TryGetProperty("error", out var errorEl) && errorEl.ValueKind == JsonValueKind.String)
            variables.TryAdd("error", errorEl.GetString() ?? string.Empty);

        if (result.TryGetProperty("errorCode", out var errorCodeEl) && errorCodeEl.ValueKind == JsonValueKind.String)
            variables.TryAdd("error.code", errorCodeEl.GetString() ?? string.Empty);
    }

    private static void ExtractHeaders(JsonElement result, string? actionResult, Dictionary<string, string> variables)
    {
        if (result.TryGetProperty("headers", out var headersArray))
        {
            var prefix = actionResult switch
            {
                "RequestMessage" or "TargetRequestMessage" => "request.header.",
                "ResponseMessage" or "TargetResponseMessage" or "ResponseMessageSent" => "response.header.",
                _ => "message.header.",
            };

            foreach (var header in headersArray.EnumerateArray())
            {
                var name = TryGetString(header, "name");
                var value = TryGetString(header, "value");
                if (name is not null && value is not null) variables.TryAdd($"{prefix}{name}", value);
            }
        }
    }

    private static void ExtractContent(JsonElement result, string? actionResult, Dictionary<string, string> variables)
    {
        if (result.TryGetProperty("content", out var bodyEl) && bodyEl.ValueKind == JsonValueKind.String)
        {
            var prefix = actionResult switch
            {
                "RequestMessage" or "TargetRequestMessage" => "request.",
                "ResponseMessage" or "TargetResponseMessage" or "ResponseMessageSent" => "response.",
                _ => "message.",
            };

            variables.TryAdd($"{prefix}content", bodyEl.GetString() ?? string.Empty);
        }
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