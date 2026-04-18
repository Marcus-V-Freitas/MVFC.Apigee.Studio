namespace MVFC.Apigee.Studio.Infrastructure.Logs;

public static partial class LogDefinitions
{
    [LoggerMessage(LogLevel.Debug, "Fetching deployments from {Url}")]
    public static partial void LogFetchDeployments(this ILogger logger, string url);

    [LoggerMessage(LogLevel.Warning, "ListDeployedApis retornou {Status}")]
    public static partial void LogListDeployedApis(this ILogger logger, HttpStatusCode status);

    [LoggerMessage(LogLevel.Warning, "StopTrace retornou {Status} para sessão '{SessionId}'")]
    public static partial void LogStopTrace(this ILogger logger, HttpStatusCode status, string sessionId);

    [LoggerMessage(LogLevel.Information, "Trace session '{SessionId}' encerrada")]
    public static partial void LogTraceSessionStopped(this ILogger logger, string sessionId);

    [LoggerMessage(LogLevel.Information, "Deploying {Zip} -> {Url}")]
    public static partial void LogDeployApi(this ILogger logger, string zip, string url);

    [LoggerMessage(LogLevel.Information, "Starting trace session for proxy '{Proxy}'")]
    public static partial void LogStartTraceSession(this ILogger logger, string proxy);

    [LoggerMessage(LogLevel.Debug, "ProxiesDir não encontrado: {Dir}")]
    public static partial void LogProxiesDirNotFound(this ILogger logger, string dir);

    [LoggerMessage(LogLevel.Debug, "Nenhum XML em {Dir}")]
    public static partial void LogNoXmlFound(this ILogger logger, string dir);

    [LoggerMessage(LogLevel.Warning, "Erro ao parsear ProxyEndpoint XML: {File}")]
    public static partial void LogErrorParsingProxyEndpointXml(this ILogger logger, Exception ex, string file);

    [LoggerMessage(LogLevel.Warning, "docker images indisponível, usando lista padrão")]
    public static partial void LogDockerImageNotAvailable(this ILogger logger, Exception ex);

    [LoggerMessage(LogLevel.Warning, "Emulator health check failed")]
    public static partial void LogEmulatorNotHealth(this ILogger logger, Exception ex);
}
