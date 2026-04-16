namespace ApigeeLocalDev.Domain.Interfaces;

/// <summary>
/// Lê os flows de um API proxy a partir dos arquivos XML no disco
/// e retorna a lista ordenada de TracePoints com as políticas definidas.
/// </summary>
public interface IBundleFlowReader
{
    /// <summary>
    /// Retorna os steps (políticas) definidos no ProxyEndpoint do proxy,
    /// em ordem de execução: PreFlow Request, Flows Request, PostFlow Request,
    /// PostFlow Response, Flows Response, PreFlow Response.
    /// </summary>
    /// <param name="workspaceRoot">Caminho raiz do workspace (ex: C:/apigee-workspaces/meu-ws)</param>
    /// <param name="proxyName">Nome do API proxy (ex: ola-mundo)</param>
    /// <param name="statusCode">Status code da resposta — usado para inferir Executed e Error</param>
    IReadOnlyList<TracePoint> ReadFlowPoints(string workspaceRoot, string proxyName, int statusCode);
}
