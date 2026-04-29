namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class ExternalToolsHelp : ComponentBase
{
    private bool _installing;
    private bool _installFinished;
    private readonly List<string> _outputLines = [];

    [Inject]
    public required IToolInstallerService ToolInstaller { get; set; }

    [Inject]
    public required IConfiguration Config { get; set; }

    private async Task InstallNow()
    {
        var toolName = Config["ExternalTools:LintTool"] ?? "apigeelint";
        _installing = true;
        _outputLines.Clear();
        StateHasChanged();

        var success = await ToolInstaller.InstallToolAsync(toolName, (line) =>
        {
            _outputLines.Add(line);
            _ = InvokeAsync(StateHasChanged);
        });

        _installing = false;
        _installFinished = success;
        StateHasChanged();
    }
}