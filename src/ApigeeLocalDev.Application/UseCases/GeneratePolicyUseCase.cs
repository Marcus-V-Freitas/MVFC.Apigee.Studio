using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Application.UseCases;

public sealed class GeneratePolicyUseCase(
    IPolicyTemplateRepository templateRepository,
    IWorkspaceRepository workspaceRepository)
{
    public async Task<string> ExecuteAsync(
        string workspaceRoot,
        string proxyName,
        string policyFolderRelative,
        string templateName,
        IDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var template = templateRepository.GetByName(templateName)
            ?? throw new InvalidOperationException($"Template '{templateName}' not found.");

        var xml = templateRepository.GeneratePolicyXml(template, parameters);
        var policyName = parameters.TryGetValue("PolicyName", out var n) ? n : templateName;
        var outputPath = Path.Combine(workspaceRoot, proxyName, policyFolderRelative, $"{policyName}.xml");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await workspaceRepository.SaveFileAsync(outputPath, xml, ct);

        return outputPath;
    }
}
