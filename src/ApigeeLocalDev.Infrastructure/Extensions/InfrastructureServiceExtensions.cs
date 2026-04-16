namespace ApigeeLocalDev.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IWorkspaceRepository, WorkspaceFileSystemRepository>();
        services.AddSingleton<IPolicyTemplateRepository, PolicyTemplateRepository>();
        services.AddSingleton<IBundleFlowReader, BundleFlowReader>();

        var managementUrl = configuration["ApigeeEmulator:BaseUrl"] ?? "http://localhost:8080";

        services.AddHttpClient<IApigeeEmulatorClient, ApigeeEmulatorClient>(client =>
        {
            client.BaseAddress = new Uri(managementUrl);
            client.Timeout     = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IApigeeTraceClient, ApigeeTraceClient>(client =>
        {
            client.BaseAddress = new Uri(managementUrl);
            client.Timeout     = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}