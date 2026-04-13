using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ApigeeLocalDev.Infrastructure.Http;

/// <summary>
/// Cliente HTTP para o Apigee Emulator local.
///
/// O emulator expõe a Management API do Apigee Edge na porta de controle (8080).
/// Endpoints válidos confirmados:
///   GET  /v1/emulator/version
///   POST /v1/organizations/{org}/apis?action=import&amp;name={proxy}
///        Body: multipart/form-data, campo "file" = ZIP bytes
///   POST /v1/organizations/{org}/environments/{env}/apis/{api}/revisions/{rev}/deployments
///        Body: vazio (application/x-www-form-urlencoded)
///
/// IMPORTANTE: o endpoint de import NÃO aceita application/octet-stream raw —
/// exige multipart/form-data com o ZIP no campo "file".
/// </summary>
public sealed class ApigeeEmulatorClient(
    HttpClient http,
    ILogger<ApigeeEmulatorClient> logger) : IApigeeEmulatorClient
{
    private const string DefaultContainerName = "apigee-emulator";
    private const string DefaultOrg           = "emulator";

    // ── Health check ──────────────────────────────────────────────────────────
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

    // ── Deploy proxy/sharedflow individual ────────────────────────────────────
    //
    // Passo 1: importar bundle ZIP como multipart/form-data, campo "file"
    //   POST /v1/organizations/{org}/apis?action=import&name={proxy}
    //   Response: { "revision": ["1"], ... }
    //
    // Passo 2: deployar revisão no environment
    //   POST /v1/organizations/{org}/environments/{env}/apis/{proxy}/revisions/{rev}/deployments
    //   Body: vazio (application/x-www-form-urlencoded)
    public async Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP não encontrado: " + zipPath);

        var proxyName = Path.GetFileNameWithoutExtension(zipPath)
                            .Split('_')[0]; // remove sufixo de timestamp gerado pelo repo

        // ── Passo 1: import via multipart/form-data ───────────────────────────
        var importUrl = "/v1/organizations/" + DefaultOrg
                      + "/apis?action=import&name=" + Uri.EscapeDataString(proxyName);

        logger.LogInformation("Importing '{Proxy}' from {Zip} -> {Url}", proxyName, zipPath, importUrl);

        string revision;
        await using (var fs = File.OpenRead(zipPath))
        {
            // O Apigee Emulator exige multipart/form-data com o ZIP no campo "file"
            var fileContent = new StreamContent(fs);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var form = new MultipartFormDataContent();
            form.Add(fileContent, "file", Path.GetFileName(zipPath));

            using var importResp = await http.PostAsync(importUrl, form, ct);
            var importBody = await importResp.Content.ReadAsStringAsync(ct);

            if (!importResp.IsSuccessStatusCode)
            {
                logger.LogError("Import failed {Status}: {Body}", importResp.StatusCode, importBody);
                throw new HttpRequestException(
                    "Import falhou (" + (int)importResp.StatusCode + "): " + importBody);
            }

            revision = ExtractRevision(importBody);
            logger.LogInformation("Imported '{Proxy}' revision {Rev}", proxyName, revision);
        }

        // ── Passo 2: deploy revision ──────────────────────────────────────────
        var deployUrl = "/v1/organizations/" + DefaultOrg
                      + "/environments/" + Uri.EscapeDataString(environment)
                      + "/apis/" + Uri.EscapeDataString(proxyName)
                      + "/revisions/" + revision
                      + "/deployments";

        logger.LogInformation("Deploying '{Proxy}' rev {Rev} to '{Env}' -> {Url}",
            proxyName, revision, environment, deployUrl);

        using var deployContent = new StringContent(string.Empty);
        deployContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        using var deployResp = await http.PostAsync(deployUrl, deployContent, ct);
        var deployBody = await deployResp.Content.ReadAsStringAsync(ct);

        if (!deployResp.IsSuccessStatusCode)
        {
            logger.LogError("Deploy failed {Status}: {Body}", deployResp.StatusCode, deployBody);
            throw new HttpRequestException(
                "Deploy falhou (" + (int)deployResp.StatusCode + "): " + deployBody);
        }

        logger.LogInformation("Deploy ok: '{Proxy}' em '{Env}'", proxyName, environment);
    }

    // ── Docker helpers ────────────────────────────────────────────────────────

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

    // ── helpers privados ──────────────────────────────────────────────────────

    /// <summary>
    /// Extrai o número da revisão do JSON de resposta do import.
    /// Formatos possíveis:
    ///   { "revision": ["1"] }           — Apigee Edge padrão
    ///   { "latestRevisionId": "1" }      — formato alternativo
    /// Fallback: "1".
    /// </summary>
    private static string ExtractRevision(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            if (root.TryGetProperty("revision", out var arr)
                && arr.ValueKind == JsonValueKind.Array)
            {
                var last = arr.EnumerateArray().LastOrDefault();
                if (last.ValueKind != JsonValueKind.Undefined)
                    return last.GetString() ?? "1";
            }

            if (root.TryGetProperty("latestRevisionId", out var lat))
                return lat.GetString() ?? "1";
        }
        catch { /* JSON inesperado — fallback abaixo */ }

        return "1";
    }

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
            throw new InvalidOperationException(
                "docker " + arguments + " failed: " + sbErr);

        if (!string.IsNullOrWhiteSpace(sbErr.ToString()))
            logger.LogDebug("docker {Args} stderr: {Err}", arguments, sbErr.ToString().Trim());

        return sbOut.ToString();
    }
}
