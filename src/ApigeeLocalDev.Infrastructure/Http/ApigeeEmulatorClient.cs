using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ApigeeLocalDev.Infrastructure.Http;

/// <summary>
/// Cliente HTTP para o Apigee Emulator local.
///
/// Endpoints do emulator (porta 8080):
///   GET  /v1/emulator/version
///   POST /v1/emulator/deploy?environment={env}
///        Body: ZIP do environment archive (src/main/apigee/...)
///        Content-Type: application/octet-stream
/// </summary>
public sealed class ApigeeEmulatorClient(
    HttpClient http,
    ILogger<ApigeeEmulatorClient> logger) : IApigeeEmulatorClient
{
    private const string DefaultContainerName = "apigee-emulator";

    // ── Health check ───────────────────────────────────────────────────────
    public async Task<bool> IsAliveAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync("/v1/emulator/version", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Deploy environment archive ──────────────────────────────────────
    //
    // POST /v1/emulator/deploy?environment={env}
    //   Body: ZIP com estrutura src/main/apigee/...
    //   Content-Type: application/octet-stream
    public async Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP não encontrado: " + zipPath);

        var url = "/v1/emulator/deploy?environment=" + Uri.EscapeDataString(environment);

        logger.LogInformation("Deploying archive {Zip} to '{Env}' -> {Url}", zipPath, environment, url);

        await using var fs = File.OpenRead(zipPath);
        using var content = new StreamContent(fs);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var resp = await http.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError("Deploy failed {Status}: {Body}", resp.StatusCode, body);
            throw new HttpRequestException(
                "Deploy falhou (" + (int)resp.StatusCode + "): " + body);
        }

        logger.LogInformation("Deploy ok: '{Env}'", environment);
    }

    // ── Docker helpers ───────────────────────────────────────────────────
    public async Task StartContainerAsync(string image, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(image))
            throw new ArgumentException("Docker image must be provided.", nameof(image));

        await RunDockerAsync("rm -f " + DefaultContainerName, ct, ignoreErrors: true);
        await RunDockerAsync(
            "run -d --name " + DefaultContainerName + " -p 8080:8080 -p 8998:8998 " + image, ct);
        logger.LogInformation("Started Apigee Emulator {Name} ({Image})", DefaultContainerName, image);
    }

    public async Task StopContainerAsync(CancellationToken ct = default)
    {
        await RunDockerAsync("rm -f " + DefaultContainerName, ct, ignoreErrors: true);
        logger.LogInformation("Stopped Apigee Emulator container {Name}", DefaultContainerName);
    }

    public async Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default)
    {
        var output = await RunDockerAsync(
            "images --format \"{{.Repository}}:{{.Tag}}\"", ct, ignoreErrors: true);

        return output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l)
            .ToList();
    }

    // ── privados ───────────────────────────────────────────────────────────
    private async Task<string> RunDockerAsync(
        string arguments, CancellationToken ct, bool ignoreErrors = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "docker",
            Arguments              = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
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

        if (proc.ExitCode != 0 && !ignoreErrors)
            throw new InvalidOperationException("docker " + arguments + " failed: " + sbErr);

        return sbOut.ToString();
    }
}
