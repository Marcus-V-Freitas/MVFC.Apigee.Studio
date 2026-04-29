namespace MVFC.Apigee.Studio.Domain.Interfaces;

/// <summary>
/// Interface for services that manage the installation of external tools.
/// </summary>
public interface IToolInstallerService
{
    /// <summary>
    /// Checks if a tool is currently installed and available.
    /// </summary>
    /// <param name="toolCommand">The command to check.</param>
    /// <returns>True if installed, false otherwise.</returns>
    Task<bool> IsToolInstalledAsync(string toolCommand);

    /// <summary>
    /// Installs a tool (e.g., via npm).
    /// </summary>
    /// <param name="toolName">The name of the tool to install.</param>
    /// <param name="onOutput">Optional callback for capturing installation output.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> InstallToolAsync(string toolName, Action<string>? onOutput = null);
}
