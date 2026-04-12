using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Application.UseCases;

public sealed class GeneratePolicyUseCase(
    IPolicyTemplateRepository templateRepository,
    IWorkspaceRepository workspaceRepository)
{
    /// <summary>
    /// Gera a política diretamente no caminho absoluto informado.
    /// Use este quando você já tem o path completo da pasta (ex: vindo da árvore de arquivos).
    /// </summary>
    public async Task<string> ExecuteAtPathAsync(
        string absolutePoliciesFolder,
        string templateName,
        IDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var template = templateRepository.GetByName(templateName)
            ?? throw new InvalidOperationException($"Template '{templateName}' not found.");

        var xml        = templateRepository.GeneratePolicyXml(template, parameters);
        var policyName = parameters.TryGetValue("PolicyName", out var n) && !string.IsNullOrWhiteSpace(n)
            ? n
            : templateName;

        var outputPath = Path.Combine(absolutePoliciesFolder, $"{policyName}.xml");
        Directory.CreateDirectory(absolutePoliciesFolder);
        await workspaceRepository.SaveFileAsync(outputPath, xml, ct);
        return outputPath;
    }

    /// <summary>
    /// Overload legado – constrói o path a partir de workspaceRoot + proxyName + subPath relativo.
    /// Mantido para compatibilidade.
    /// </summary>
    public Task<string> ExecuteAsync(
        string workspaceRoot,
        string proxyName,
        string policyFolderRelative,
        string templateName,
        IDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var absoluteFolder = Path.Combine(workspaceRoot, proxyName, policyFolderRelative);
        return ExecuteAtPathAsync(absoluteFolder, templateName, parameters, ct);
    }
}
