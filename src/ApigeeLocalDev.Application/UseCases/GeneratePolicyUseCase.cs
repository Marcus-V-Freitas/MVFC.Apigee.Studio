namespace ApigeeLocalDev.Application.UseCases;

/// <summary>
/// Use case for generating policy XML files from templates and saving them to the workspace.
/// </summary>
public sealed class GeneratePolicyUseCase(
    IPolicyTemplateRepository templateRepository,
    IWorkspaceRepository workspaceRepository)
{
    /// <summary>
    /// Generates a policy XML file at the specified absolute folder path.
    /// Use this method when you already have the complete absolute path to the target folder (e.g., from a file tree).
    /// </summary>
    /// <param name="absolutePoliciesFolder">The absolute path to the folder where the policy file will be saved.</param>
    /// <param name="templateName">The name of the policy template to use.</param>
    /// <param name="parameters">A dictionary containing parameters to be injected into the template.</param>
    /// <param name="forcedXmlContent">Optional. If provided, this XML content will be used instead of generating from the template.</param>
    /// <param name="ct">Optional. Cancellation token for the operation.</param>
    /// <returns>The absolute path to the generated policy XML file.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified template is not found.</exception>
    public async Task<string> ExecuteAtPathAsync(
        string absolutePoliciesFolder,
        string templateName,
        IDictionary<string, string> parameters,
        string? forcedXmlContent = null,
        CancellationToken ct = default)
    {
        var template = templateRepository.GetByName(templateName)
            ?? throw new InvalidOperationException($"Template '{templateName}' not found.");

        var xmlContent = !string.IsNullOrWhiteSpace(forcedXmlContent)
            ? forcedXmlContent
            : templateRepository.GeneratePolicyXml(template, parameters);

        var policyName = parameters.TryGetValue("PolicyName", out var policyNameValue) && !string.IsNullOrWhiteSpace(policyNameValue)
            ? policyNameValue
            : templateName;

        var outputPath = Path.Combine(absolutePoliciesFolder, $"{policyName}.xml");
        Directory.CreateDirectory(absolutePoliciesFolder);
        await workspaceRepository.SaveFileAsync(outputPath, xmlContent, ct);

        return outputPath;
    }

    /// <summary>
    /// Legacy overload. Constructs the absolute folder path from workspace root, proxy name, and a relative subfolder path.
    /// Maintained for backward compatibility.
    /// </summary>
    /// <param name="workspaceRoot">The root path of the workspace.</param>
    /// <param name="proxyName">The name of the API proxy.</param>
    /// <param name="policyFolderRelative">The relative path to the policy folder within the proxy.</param>
    /// <param name="templateName">The name of the policy template to use.</param>
    /// <param name="parameters">A dictionary containing parameters to be injected into the template.</param>
    /// <param name="forcedXmlContent">Optional. If provided, this XML content will be used instead of generating from the template.</param>
    /// <param name="ct">Optional. Cancellation token for the operation.</param>
    /// <returns>The absolute path to the generated policy XML file.</returns>
    public Task<string> ExecuteAsync(
        string workspaceRoot,
        string proxyName,
        string policyFolderRelative,
        string templateName,
        IDictionary<string, string> parameters,
        string? forcedXmlContent = null,
        CancellationToken ct = default)
    {
        var absoluteFolder = Path.Combine(workspaceRoot, proxyName, policyFolderRelative);

        return ExecuteAtPathAsync(absoluteFolder, templateName, parameters, forcedXmlContent, ct);
    }
}
