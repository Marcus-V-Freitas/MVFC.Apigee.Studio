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

        services.AddHttpClient<IApigeeEmulatorClient, ApigeeEmulatorClient>(client =>
        {
            var baseUrl = configuration["ApigeeEmulator:BaseUrl"] ?? "http://localhost:8080";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
