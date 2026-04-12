using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ApigeeLocalDev.Infrastructure.Http;

public sealed class ApigeeEmulatorClient(
    HttpClient http,
    ILogger<ApigeeEmulatorClient> logger) : IApigeeEmulatorClient
{
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
}
