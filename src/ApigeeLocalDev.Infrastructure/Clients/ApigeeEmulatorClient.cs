using System.Diagnostics;
using System.Net.Http.Headers;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApigeeLocalDev.Infrastructure.Clients;

/// <summary>
/// Cliente HTTP para o Apigee Emulator rodando localmente em Docker.
///
/// Endpoints usados:
///   GET  /v1/emulator/healthz              -> liveness check
///   POST /v1/emulator/deploy?environment=  -> bundle deploy (apiproxy/ na raiz do ZIP)
///   POST /v1/emulator/deployArchive?environment= -> archive deploy (src/main/apigee/...)
/// </summary>
public sealed class ApigeeEmulatorClient(
    HttpClient http,
    IConfiguration config,
    ILogger<ApigeeEmulatorClient> logger) : IApigeeEmulatorClient
{
    public async Task<bool> IsAliveAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await http.GetAsync("/v1/emulator/healthz", ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Emulator health check failed");
            return false;
        }
    }

    /// <summary>
    /// Bundle deploy: ZIP com apiproxy/ ou sharedflowbundle/ na raiz.
    /// </summary>
    public async Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default)
    {
        await PostZipAsync(
            "/v1/emulator/deploy?environment=" + Uri.EscapeDataString(environment),
            zipPath,
            ct);
    }

    /// <summary>
    /// Archive deploy: ZIP no formato src/main/apigee/... (workspace completo).
    /// </summary>
    public async Task DeployArchiveAsync(string environment, string zipPath, CancellationToken ct = default)
    {
        await PostZipAsync(
            "/v1/emulator/deployArchive?environment=" + Uri.EscapeDataString(environment),
            zipPath,
            ct);
    }

    private async Task PostZipAsync(string url, string zipPath, CancellationToken ct)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP n\u00e3o encontrado: " + zipPath);

        await using var fs      = File.OpenRead(zipPath);
        using var       content = new StreamContent(fs);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        logger.LogInformation("Deploying {Zip} -> {Url}", zipPath, url);
        var response = await http.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                "Deploy falhou (" + (int)response.StatusCode + " " +
                response.StatusCode + "): " + body);
        }
    }

    public Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default)
    {
        var images = new List<string>
        {
            "gcr.io/apigee-release/hybrid/apigee-emulator:latest",
            "gcr.io/apigee-release/hybrid/apigee-emulator:1.12.0",
            "gcr.io/apigee-release/hybrid/apigee-emulator:1.11.0",
            "gcr.io/apigee-release/hybrid/apigee-emulator:1.10.0"
        };

        // Tenta complementar com imagens Docker instaladas localmente
        try
        {
            var psi = new ProcessStartInfo("docker", "images --format \"{{.Repository}}:{{.Tag}}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                var local = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(l => l.Contains("apigee", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var img in local)
                    if (!images.Contains(img))
                        images.Add(img);
            }
        }
        catch { /* docker n\u00e3o instalado ou acess\u00edvel */ }

        return Task.FromResult<IReadOnlyList<string>>(images);
    }

    public async Task StartContainerAsync(string image, CancellationToken ct = default)
    {
        var port    = config["ApigeeEmulator:Port"] ?? "8080";
        var args    = "run -d --rm -p " + port + ":8080 --name apigee-emulator " + image;
        await RunDockerAsync(args, ct);
    }

    public async Task StopContainerAsync(CancellationToken ct = default)
        => await RunDockerAsync("stop apigee-emulator", ct);

    private static async Task RunDockerAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("docker", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("N\u00e3o foi poss\u00edvel iniciar o processo docker.");

        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException("docker " + args + " falhou: " + err);
        }
    }
}
