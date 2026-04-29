namespace MVFC.Apigee.Studio.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering infrastructure services in the dependency injection container.
/// Provides registration for repositories, readers, and HTTP clients used by the application.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers infrastructure services, repositories, and HTTP clients for Apigee Emulator integration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration instance.</param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IWorkspaceRepository, WorkspaceFileSystemRepository>();
        services.AddSingleton<IPolicyTemplateRepository, PolicyTemplateRepository>();
        services.AddSingleton<IBundleFlowReader, BundleFlowReader>();
        services.AddSingleton<IPolicyValidator, ApigeeXmlPolicyValidator>();
        services.AddScoped<IBundleSnapshotRepository, BundleSnapshotRepository>();
        services.AddSingleton<IApigeeLintRunner, ApigeeLintRunner>();
        services.AddSingleton<IRenameRefactoringService, XmlRenameRefactoringService>();
        services.AddSingleton<IBundleLinter, BundleLinter>();
        services.AddSingleton<IToolInstallerService, ToolInstallerService>();

        var managementUrl = configuration["ApigeeEmulator:BaseUrl"] ?? new UriBuilder(Uri.UriSchemeHttp, "localhost", 8080).ToString();

        services.AddHttpClient<IApigeeEmulatorClient, ApigeeEmulatorClient>(client =>
        {
            client.BaseAddress = new Uri(managementUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddHttpClient<IApigeeTraceClient, ApigeeTraceClient>(client =>
        {
            client.BaseAddress = new Uri(managementUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        return services;
    }
}