namespace MVFC.Apigee.Studio.Blazor.Services;

public static class ApigeeLintService
{
    public static async Task<IList<ApigeeLintResult>> RunLintAsync(ApigeeWorkspace workspace, string? filterFilePath = null)
    {
        var results = new List<ApigeeLintResult>();

        try
        {
            var proxyDir = Path.Combine(workspace.RootPath, "src", "main", "apigee", "apiproxies");

            if (!Directory.Exists(proxyDir))
                return results;

            var startInfo = CreateProcessStartInfo(workspace.RootPath, proxyDir);

            using var process = Process.Start(startInfo);
            if (process == null)
                return results;

            var (output, _, timedOut) = await ReadProcessOutputAsync(process);
            if (timedOut || string.IsNullOrWhiteSpace(output))
                return results;

            var parsed = TryParseLintResults(output);
            if (parsed != null)
                results.AddRange(parsed);
        }
        catch
        {
            // Apigeelint not installed or other error. Ignore to not crash UI.
        }

        if (!string.IsNullOrEmpty(filterFilePath))
        {
            return [.. results
                .Where(r => r.FilePath.Replace("\\", "/", StringComparison.OrdinalIgnoreCase)
                             .EndsWith(
                                 Path.GetFileName(filterFilePath),
                                 StringComparison.OrdinalIgnoreCase)),];
        }

        return results;
    }

    private static ProcessStartInfo CreateProcessStartInfo(string rootPath, string proxyDir)
    {
        return new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "apigeelint",
            Arguments = OperatingSystem.IsWindows()
                ? $"/c apigeelint -s \"{proxyDir}\" -f json"
                : $"-s \"{proxyDir}\" -f json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = rootPath,
        };
    }

    private static async Task<(string output, string error, bool timedOut)> ReadProcessOutputAsync(Process process)
    {
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(Task.WhenAll(outputTask, errorTask), timeoutTask);

        if (completedTask == timeoutTask)
        {
            process.Kill();
            return (string.Empty, string.Empty, true);
        }

        return (await outputTask, await errorTask, false);
    }

    private static List<ApigeeLintResult>? TryParseLintResults(string output)
    {
        var results = new List<ApigeeLintResult>();
        try
        {
            var parsedResults = JsonSerializer.Deserialize<JsonElement>(output);
            if (parsedResults.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var fileResult in parsedResults.EnumerateArray())
            {
                var filePath = fileResult.GetProperty("filePath").GetString() ?? "";
                var messagesElement = fileResult.GetProperty("messages");

                var res = new ApigeeLintResult { FilePath = filePath };

                if (messagesElement.ValueKind != JsonValueKind.Array)
                {
                    results.Add(res);
                    continue;
                }

                foreach (var msg in messagesElement.EnumerateArray())
                {
                    res.Messages.Add(new ApigeeLintMessage
                    {
                        Line = ExtractLine(msg),
                        Column = ExtractColumn(msg),
                        Message = ExtractMessage(msg),
                        Severity = ExtractSeverity(msg),
                    });
                }

                results.Add(res);
            }
            return results;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the line number from a lint message JSON element.
    /// Returns 1 if the property is missing or invalid.
    /// </summary>
    /// <param name="msg">The JSON element representing the lint message.</param>
    /// <returns>The extracted line number, or 1 if not found.</returns>
    private static int ExtractLine(JsonElement msg)
    {
        if (msg.TryGetProperty("line", out var l))
        {
            if (l.ValueKind == JsonValueKind.Number)
                return l.GetInt32();
            if (int.TryParse(l.GetString(), CultureInfo.InvariantCulture, out var li))
                return li;
        }
        return 1;
    }

    /// <summary>
    /// Extracts the column number from a lint message JSON element.
    /// Returns 1 if the property is missing or invalid.
    /// </summary>
    /// <param name="msg">The JSON element representing the lint message.</param>
    /// <returns>The extracted column number, or 1 if not found.</returns>
    private static int ExtractColumn(JsonElement msg)
    {
        if (msg.TryGetProperty("column", out var c))
        {
            if (c.ValueKind == JsonValueKind.Number)
                return c.GetInt32();
            if (int.TryParse(c.GetString(), CultureInfo.InvariantCulture, out var ci))
                return ci;
        }
        return 1;
    }

    /// <summary>
    /// Extracts the message text from a lint message JSON element.
    /// Returns an empty string if the property is missing.
    /// </summary>
    /// <param name="msg">The JSON element representing the lint message.</param>
    /// <returns>The extracted message text, or an empty string if not found.</returns>
    private static string ExtractMessage(JsonElement msg) =>
        msg.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";

    /// <summary>
    /// Extracts the severity from a lint message JSON element.
    /// Returns 2 if the property is missing or invalid.
    /// </summary>
    /// <param name="msg">The JSON element representing the lint message.</param>
    /// <returns>The extracted severity, or 2 if not found.</returns>
    private static int ExtractSeverity(JsonElement msg)
    {
        if (msg.TryGetProperty("severity", out var s) && s.ValueKind == JsonValueKind.Number)
        {
            return s.GetInt32();
        }

        return 2;
    }
}
