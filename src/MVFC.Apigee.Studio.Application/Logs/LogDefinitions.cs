namespace MVFC.Apigee.Studio.Application.Logs;

public static partial class LogDefinitions
{
    [LoggerMessage(LogLevel.Information, "Enviando recursos de teste (produtos, desenvolvedores, apps) para o emulador...")]
    public static partial void LogDeployingTestResources(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Recursos de teste enviados com sucesso.")]
    public static partial void LogTestResourcesDeployed(this ILogger logger);
}