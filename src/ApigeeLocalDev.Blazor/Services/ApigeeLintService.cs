namespace ApigeeLocalDev.Blazor.Services;

public class ApigeeLintResult
{
    public string FilePath { get; set; } = "";
    public List<ApigeeLintMessage> Messages { get; set; } = [];
}

public class ApigeeLintMessage
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = "";
    public int Severity { get; set; } // 1 = warning, 2 = error (apigeelint convention)
}

public class ApigeeLintService
{
    public async Task<List<ApigeeLintResult>> RunLintAsync(ApigeeWorkspace workspace)
    {
        var results = new List<ApigeeLintResult>();
        
        try
        {
            var proxyDir = Path.Combine(workspace.RootPath, "src", "main", "apigee", "apiproxies");
            
            if (!Directory.Exists(proxyDir)) 
                return results;

            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "apigeelint",
                Arguments = OperatingSystem.IsWindows() ? $"/c apigeelint -s \"{proxyDir}\" -f json" : $"-s \"{proxyDir}\" -f json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workspace.RootPath,
            };

            using var process = Process.Start(startInfo);
            if (process == null) 
                return results;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(Task.WhenAll(outputTask, errorTask), timeoutTask);

            if (completedTask == timeoutTask)
            {
                process.Kill();
                return results; // Timeout
            }

            var output = await outputTask;
            
            if (string.IsNullOrWhiteSpace(output)) 
                return results;

            // apigeelint returns an array of file results in JSON
            try
            {
                var parsedResults = JsonSerializer.Deserialize<JsonElement>(output);
                if (parsedResults.ValueKind != JsonValueKind.Array)
                {
                    return results;
                }

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
                            Line = msg.TryGetProperty("line", out var l) ? (l.ValueKind == JsonValueKind.Number ? l.GetInt32() : (int.TryParse(l.GetString(), out var li) ? li : 1)) : 1,
                            Column = msg.TryGetProperty("column", out var c) ? (c.ValueKind == JsonValueKind.Number ? c.GetInt32() : (int.TryParse(c.GetString(), out var ci) ? ci : 1)) : 1,
                            Message = msg.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "",
                            Severity = msg.TryGetProperty("severity", out var s) ? (s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 2) : 2
                        });
                    }

                    results.Add(res);
                }

                return results;
            }
            catch (JsonException) { }
        }
        catch 
        { 
            // Apigeelint not installed or other error. Ignore to not crash UI.
        }

        return results;
    }
}
