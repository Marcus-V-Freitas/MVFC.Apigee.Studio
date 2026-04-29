namespace MVFC.Apigee.Studio.Infrastructure.Services;

/// <summary>
/// Service responsible for checking and installing external tools like apigeelint.
/// </summary>
public sealed class ToolInstallerService : IToolInstallerService
{
    /// <summary>
    /// Checks if a tool is installed and available in the system's PATH.
    /// </summary>
    /// <param name="toolCommand">The command to check (e.g., 'apigeelint').</param>
    /// <returns>True if the tool is found, false otherwise.</returns>
    public async Task<bool> IsToolInstalledAsync(string toolCommand)
    {
        try
        {
            var fileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
            var arguments = OperatingSystem.IsWindows() ? $"/c where {toolCommand}" : $"-c \"command -v {toolCommand}\"";

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process == null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to install a tool using npm.
    /// </summary>
    /// <param name="toolName">The name of the tool to install.</param>
    /// <param name="onOutput">Action to handle output lines from the installation process.</param>
    /// <returns>True if the installation was successful, false otherwise.</returns>
    public async Task<bool> InstallToolAsync(string toolName, Action<string>? onOutput = null)
    {
        try
        {
            var fileName = OperatingSystem.IsWindows() ? "cmd.exe" : "npm";
            var arguments = OperatingSystem.IsWindows() ? $"/c chcp 65001 > nul && npm install -g {toolName}" : $"install -g {toolName}";

            onOutput?.Invoke($"Iniciando instalação de {toolName} via npm...");

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process == null)
                return false;

            process.OutputDataReceived += (_, e) => { if (e.Data != null) onOutput?.Invoke(CleanOutput(e.Data)); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) onOutput?.Invoke(CleanOutput(e.Data)); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            onOutput?.Invoke($"Erro fatal na instalação: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Cleans the terminal output by removing noise and trimming whitespace.
    /// </summary>
    private static string CleanOutput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Remove noise like active code page message if it leaks
        if (input.Contains("65001", StringComparison.Ordinal) || input.Contains("Página de código", StringComparison.Ordinal))
            return string.Empty;

        return input.Trim();
    }
}
