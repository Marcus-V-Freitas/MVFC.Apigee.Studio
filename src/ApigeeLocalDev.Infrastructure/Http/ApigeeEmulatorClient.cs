using System.Diagnostics;
using System.Text;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ApigeeLocalDev.Infrastructure.Http;

public sealed class ApigeeEmulatorClient(
    HttpClient http,
    ILogger<ApigeeEmulatorClient> logger) : IApigeeEmulatorClient
{
    private const string DefaultContainerName = "apigee-emulator";

    public async Task DeployBundleAsync(string environment, string zipFilePath, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(zipFilePath);
        using var content = new StreamContent(fs);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

        var url = $"/v1/emulator/deploy?environment={Uri.EscapeDataString(environment)}";
        logger.LogInformation("Deploying bundle from {ZipPath} to environment {Env}", zipFilePath, environment);

        using var response = await http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        logger.LogInformation("Deploy successful: {StatusCode}", response.StatusCode);
    }

    public async Task<bool> IsAliveAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync("/v1/emulator/status", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task StartContainerAsync(string image, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(image))
            throw new ArgumentException("Docker image must be provided.", nameof(image));

        // Garante que não existe um container antigo com o mesmo nome.
        await RunDockerAsync($"rm -f {DefaultContainerName}", ct, ignoreErrors: true);

        var args = $"run -d --name {DefaultContainerName} -p 8080:8080 {image}";
        await RunDockerAsync(args, ct);
        logger.LogInformation("Started Apigee Emulator container {Name} with image {Image}", DefaultContainerName, image);
    }

    public async Task StopContainerAsync(CancellationToken ct = default)
    {
        await RunDockerAsync($"rm -f {DefaultContainerName}", ct, ignoreErrors: true);
        logger.LogInformation("Stopped Apigee Emulator container {Name}", DefaultContainerName);
    }

    public async Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default)
    {
        var output = await RunDockerAsync("images --format \"{{.Repository}}:{{.Tag}}\"", ct, ignoreErrors: true);
        var lines = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l)
            .ToList();
        return lines;
    }

    private async Task<string> RunDockerAsync(string arguments, CancellationToken ct, bool ignoreErrors = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = false };
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();

        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) sbOut.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) sbErr.AppendLine(e.Data); };

        if (!proc.Start())
            throw new InvalidOperationException("Failed to start docker process.");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(ct);

        var stdout = sbOut.ToString();
        var stderr = sbErr.ToString();

        if (proc.ExitCode != 0 && !ignoreErrors)
            throw new InvalidOperationException($"docker {arguments} failed: {stderr}");

        if (!string.IsNullOrWhiteSpace(stderr))
            logger.LogDebug("docker {Args} stderr: {Err}", arguments, stderr.Trim());

        return stdout;
    }
}
