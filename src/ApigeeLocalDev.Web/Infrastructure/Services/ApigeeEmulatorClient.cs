using ApigeeLocalDev.Web.Application.Services;
using Microsoft.Extensions.Options;

namespace ApigeeLocalDev.Web.Infrastructure.Services;

public sealed class ApigeeEmulatorOptions
{
    public const string SectionName = "ApigeeEmulator";
    public string BaseUrl { get; set; } = "http://localhost:8080";
}

public sealed class ApigeeEmulatorClient : IApigeeEmulatorClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApigeeEmulatorClient> _logger;

    public ApigeeEmulatorClient(HttpClient httpClient, ILogger<ApigeeEmulatorClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> DeployAsync(string environment, string zipPath)
    {
        if (!File.Exists(zipPath))
        {
            _logger.LogWarning("ZIP file not found: {ZipPath}", zipPath);
            return false;
        }

        var url = $"/v1/emulator/deploy?environment={Uri.EscapeDataString(environment)}";

        try
        {
            await using var stream = File.OpenRead(zipPath);
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

            var response = await _httpClient.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Deployed to environment '{Environment}' successfully.", environment);
                return true;
            }

            _logger.LogWarning("Deploy returned {StatusCode} for environment '{Environment}'.",
                response.StatusCode, environment);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying to environment '{Environment}'.", environment);
            return false;
        }
    }
}
