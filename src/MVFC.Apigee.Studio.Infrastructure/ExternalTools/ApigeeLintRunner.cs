namespace MVFC.Apigee.Studio.Infrastructure.ExternalTools;

/// <summary>
/// Implementation of <see cref="IApigeeLintRunner"/> that executes the apigeelint CLI.
/// </summary>
public sealed class ApigeeLintRunner : IApigeeLintRunner
{
    /// <summary>
    /// Runs the apigeelint tool on the specified workspace.
    /// </summary>
    /// <param name="workspace">The Apigee workspace.</param>
    /// <param name="filterFilePath">Optional path to filter results for a specific file.</param>
    /// <returns>A list of linting results.</returns>
    public async Task<IList<ApigeeLintResult>> RunLintAsync(ApigeeWorkspace workspace, string? filterFilePath = null)
    {
        var results = new List<ApigeeLintResult>();
        try
        {
            var targetDirs = new List<string>();
            if (!string.IsNullOrEmpty(filterFilePath))
            {
                var fullPath = Path.IsPathRooted(filterFilePath) ? filterFilePath : Path.Combine(workspace.RootPath, filterFilePath);
                if (Directory.Exists(fullPath))
                    targetDirs.Add(fullPath);
            }

            if (targetDirs.Count == 0)
            {
                targetDirs.AddRange(ResolveProxyDirectories(workspace.RootPath).Distinct(StringComparer.OrdinalIgnoreCase));
            }

            foreach (var proxyDir in targetDirs)
            {
                var currentDir = proxyDir;

                // apigeelint works best when pointed at the directory CONTAINING apiproxy or sharedflowbundle
                if (Path.GetFileName(currentDir).Equals("apiproxy", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(currentDir).Equals("sharedflowbundle", StringComparison.OrdinalIgnoreCase))
                {
                    currentDir = Path.GetDirectoryName(currentDir);
                }

                if (currentDir == null || !Directory.Exists(currentDir))
                    continue;

                var startInfo = CreateProcessStartInfo(workspace.RootPath, currentDir);
                using var process = Process.Start(startInfo);
                if (process == null)
                    continue;

                var (output, error, timedOut) = await ReadProcessOutputAsync(process);
                var runError = HandleProcessExit(output, error, timedOut);
                if (runError != null)
                {
                    results.Add(runError);
                    continue;
                }

                var parsed = TryParseLintResults(output);
                if (parsed != null)
                    results.AddRange(parsed);
            }
        }
        catch (Exception ex)
        {
            results.Add(new ApigeeLintResult("CLI", [new ApigeeLintMessage(1, 1, $"Unexpected error running apigeelint: {ex.Message}", 2)]));
        }

        return FilterResults(results, filterFilePath);
    }

    /// <summary>
    /// Resolves all proxy and shared flow directories by searching for standard Apigee bundle structures.
    /// </summary>
    private static IEnumerable<string> ResolveProxyDirectories(string rootPath)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 5 };
        var bundles = Directory.EnumerateDirectories(rootPath, "apiproxy", options)
            .Concat(Directory.EnumerateDirectories(rootPath, "sharedflowbundle", options));

        foreach (var bundle in bundles)
        {
            var parent = Path.GetDirectoryName(bundle);
            if (parent != null)
                yield return parent;
        }
    }

    /// <summary>
    /// Handles the process exit state and checks for common errors.
    /// </summary>
    private static ApigeeLintResult? HandleProcessExit(string output, string error, bool timedOut)
    {
        if (timedOut)
            return new ApigeeLintResult("CLI", [new ApigeeLintMessage(1, 1, "apigeelint timed out.", 2)]);

        if (string.IsNullOrWhiteSpace(output) && !string.IsNullOrWhiteSpace(error))
        {
            var cleanError = CleanOutput(error);
            if (string.IsNullOrWhiteSpace(cleanError))
                return null;

            if (cleanError.Contains("apigeelint", StringComparison.OrdinalIgnoreCase) && (cleanError.Contains("not recognized", StringComparison.OrdinalIgnoreCase) || cleanError.Contains("not found", StringComparison.OrdinalIgnoreCase) || cleanError.Contains("não é reconhecido", StringComparison.OrdinalIgnoreCase)))
                return new ApigeeLintResult("CLI", [new ApigeeLintMessage(1, 1, "apigeelint is not installed. Please install it with 'npm install -g apigeelint'.", 2)]);

            return new ApigeeLintResult("CLI", [new ApigeeLintMessage(1, 1, $"apigeelint error: {cleanError}", 2)]);
        }
        return null;
    }

    /// <summary>
    /// Filters the results based on the target file path.
    /// </summary>
    private static IList<ApigeeLintResult> FilterResults(IList<ApigeeLintResult> results, string? filterFilePath)
    {
        if (string.IsNullOrEmpty(filterFilePath))
            return results;

        var fileName = Path.GetFileName(filterFilePath);
        return [.. results
            .Where(r => r.FilePath.Replace("\\", "/", StringComparison.OrdinalIgnoreCase)
                         .EndsWith(fileName, StringComparison.OrdinalIgnoreCase)),];
    }

    /// <summary>
    /// Creates the process start information for executing apigeelint.
    /// </summary>
    private static ProcessStartInfo CreateProcessStartInfo(string rootPath, string proxyDir)
    {
        return new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "apigeelint",
            Arguments = OperatingSystem.IsWindows()
                ? $"/c chcp 65001 > nul && apigeelint -s \"{proxyDir}\" -f json"
                : $"-s \"{proxyDir}\" -f json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = rootPath,
        };
    }

    /// <summary>
    /// Reads the output and error streams from the process asynchronously with a timeout.
    /// </summary>
    private static async Task<(string output, string error, bool timedOut)> ReadProcessOutputAsync(Process process)
    {
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(Task.WhenAll(outputTask, errorTask), timeoutTask);

        if (completedTask == timeoutTask)
        {
            try { process.Kill(); } catch { /* Ignore errors during process termination */ }
            return (string.Empty, string.Empty, true);
        }

        return (await outputTask, await errorTask, false);
    }

    /// <summary>
    /// Attempts to parse the JSON output from apigeelint into structured results.
    /// </summary>
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

                var messages = new List<ApigeeLintMessage>();
                if (messagesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var msg in messagesElement.EnumerateArray())
                    {
                        messages.Add(new ApigeeLintMessage(
                            ExtractLine(msg),
                            ExtractColumn(msg),
                            ExtractMessage(msg),
                            ExtractSeverity(msg)
                        ));
                    }
                }

                results.Add(new ApigeeLintResult(filePath, messages));
            }
            return results;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the line number from a JSON message element.
    /// </summary>
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
    /// Extracts the column number from a JSON message element.
    /// </summary>
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
    /// Extracts the message text from a JSON message element.
    /// </summary>
    private static string ExtractMessage(JsonElement msg) =>
        msg.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";

    /// <summary>
    /// Extracts the severity level from a JSON message element.
    /// </summary>
    private static int ExtractSeverity(JsonElement msg)
    {
        if (msg.TryGetProperty("severity", out var s) && s.ValueKind == JsonValueKind.Number)
        {
            return s.GetInt32();
        }
        return 2;
    }

    /// <summary>
    /// Cleans the terminal output by removing noise and trimming whitespace.
    /// </summary>
    private static string CleanOutput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Remove noise from chcp and trim
        return input.Replace("Página de código ativa: 65001", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("Active code page: 65001", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();
    }
}
