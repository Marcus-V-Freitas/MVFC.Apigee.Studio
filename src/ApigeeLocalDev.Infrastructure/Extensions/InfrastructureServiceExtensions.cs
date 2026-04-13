using ApigeeLocalDev.Domain.Interfaces;
using ApigeeLocalDev.Infrastructure.Http;
using ApigeeLocalDev.Infrastructure.Repositories;
using ApigeeLocalDev.Infrastructure.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApigeeLocalDev.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IWorkspaceRepository, WorkspaceFileSystemRepository>();
        services.AddSingleton<IPolicyTemplateRepository, PolicyTemplateRepository>();

        var baseUrl = configuration["ApigeeEmulator:BaseUrl"] ?? "http://localhost:8080";

        services.AddHttpClient<IApigeeEmulatorClient, ApigeeEmulatorClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout     = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IApigeeTraceClient, ApigeeTraceClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout     = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
