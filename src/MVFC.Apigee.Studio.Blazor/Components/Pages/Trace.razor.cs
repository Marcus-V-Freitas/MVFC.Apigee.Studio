namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

/// <summary>
/// Blazor page component for tracing API proxy transactions using the Apigee Emulator.
/// Allows the user to select a workspace and proxy, start and stop trace sessions, and view captured transactions in real time.
/// </summary>
public partial class Trace : ComponentBase, IAsyncDisposable
{
    /// <summary>
    /// Represents the background polling task that retrieves trace transactions while a trace session is active.
    /// </summary>
    private Task? _pollTask;

    /// <summary>
    /// The currently active tab in the UI ("trace" by default).
    /// </summary>
    private string _activeTab = "trace";

    /// <summary>
    /// The name of the selected API proxy.
    /// </summary>
    private string _proxyName = string.Empty;

    /// <summary>
    /// Indicates if a trace session is currently active.
    /// </summary>
    private bool _isTracing;

    /// <summary>
    /// Indicates if a loading operation is in progress (e.g., starting trace).
    /// </summary>
    private bool _loading;

    /// <summary>
    /// Stores the last status or error message encountered during trace operations.
    /// </summary>
    private string? _error;

    /// <summary>
    /// Indicates if the message in _error is an error (red) or info (blue).
    /// </summary>
    private bool _isError;

    /// <summary>
    /// The path of the selected workspace.
    /// </summary>
    private string? _selectedWorkspacePath;

    /// <summary>
    /// The current trace session, if any.
    /// </summary>
    private TraceSession? _session;

    /// <summary>
    /// Cancellation token source for polling trace transactions.
    /// </summary>
    private CancellationTokenSource? _pollCts;

    /// <summary>
    /// List of captured trace transactions for the current session.
    /// </summary>
    private List<TraceTransaction> _transactions = [];

    /// <summary>
    /// List of available workspaces.
    /// </summary>
    private IReadOnlyList<ApigeeWorkspace> _workspaces = [];

    /// <summary>
    /// List of available proxies for the selected workspace.
    /// </summary>
    private IReadOnlyList<string> _proxies = [];

    /// <summary>
    /// Client for communicating with the Apigee Emulator.
    /// </summary>
    [Inject]
    public required IApigeeEmulatorClient Emulator { get; set; }

    /// <summary>
    /// Repository for workspace and proxy operations.
    /// </summary>
    [Inject]
    public required IWorkspaceRepository WorkspaceRepo { get; set; }

    /// <summary>
    /// Service for displaying toast notifications.
    /// </summary>
    [Inject]
    public required ToastService Toast { get; set; }

    /// <summary>
    /// Client for listing deployed APIs.
    /// </summary>
    [Inject]
    public required IApigeeTraceClient TraceClient { get; set; }

    /// <summary>
    /// Use case for standard deployment to the emulator.
    /// </summary>
    [Inject]
    public required DeployToEmulatorUseCase DeployUseCase { get; set; }

    /// <summary>
    /// Loads the list of workspaces on component initialization.
    /// </summary>
    protected override void OnInitialized() =>
        _workspaces = WorkspaceRepo.ListAll();

    /// <summary>
    /// Handles workspace selection changes and loads proxies for the selected workspace.
    /// </summary>
    /// <param name="e">The change event arguments.</param>
    private void OnWorkspaceChanged(ChangeEventArgs e)
    {
        _selectedWorkspacePath = e.Value?.ToString();
        _proxyName = string.Empty;
        _proxies = [];

        var ws = _workspaces.FirstOrDefault(w => string.Equals(w.RootPath, _selectedWorkspacePath, StringComparison.OrdinalIgnoreCase));
        if (ws is not null)
        {
            _proxies = WorkspaceRepo.ListApiProxies(ws);
        }
    }

    /// <summary>
    /// Handles proxy selection changes.
    /// </summary>
    /// <param name="e">The change event arguments.</param>
    private void OnProxyChanged(ChangeEventArgs e) =>
        _proxyName = e.Value?.ToString() ?? string.Empty;

    /// <summary>
    /// Indicates if the workspace is not ready for tracing (not selected or tracing in progress).
    /// </summary>
    private bool IsWorkspaceNotReady =>
        string.IsNullOrEmpty(_selectedWorkspacePath) || _isTracing;

    /// <summary>
    /// Starts a trace session for the selected proxy.
    /// If the proxy is not yet deployed, it performs an automatic deploy using the standard workflow.
    /// </summary>
    private async Task StartTrace()
    {
        _error = null;
        _isError = false;
        _loading = true;

        _pollCts = new CancellationTokenSource();
        var ct = _pollCts.Token;

        StateHasChanged();

        try
        {
            var deployedApis = await TraceClient.ListDeployedApisAsync("local", ct);
            var isDeployed = deployedApis.Any(a => string.Equals(a.ApiProxy, _proxyName, StringComparison.OrdinalIgnoreCase));

            if (!isDeployed)
            {
                var ws = _workspaces.FirstOrDefault(w => string.Equals(w.RootPath, _selectedWorkspacePath, StringComparison.OrdinalIgnoreCase));
                if (ws == null)
                {
                    _error = "Workspace não encontrado para realizar o deploy automático.";
                    _isError = true;
                    return;
                }

                _error = $"Realizando deploy automático de '{_proxyName}'...";
                _isError = false;
                StateHasChanged();

                await DeployUseCase.ExecuteAsync(ws, _proxyName, "local", ct);
                await Task.Delay(500, ct);
                _error = null;
            }

            _session = await Emulator.StartTraceAsync(_proxyName, ct);
            _isTracing = true;
            _transactions.Clear();

            _pollTask = PollLoopAsync(ct);

            Toast.ShowSuccess($"Trace iniciado para '{_proxyName}'");
        }
        catch (OperationCanceledException)
        {
            _error = "Operação cancelada.";
            _isError = false;
        }
        catch (Exception ex)
        {
            _error = "Falha ao iniciar trace: " + ex.Message;
            _isError = true;
            Toast.ShowError(_error);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Stops the current trace session and polling.
    /// </summary>
    private async Task StopTrace()
    {
        await StopTraceInternalAsync();
        Toast.ShowSuccess("Trace encerrado.");
    }

    /// <summary>
    /// Stops the trace session and cancels polling (internal logic).
    /// </summary>
    private async Task StopTraceInternalAsync()
    {
        await CancelPollAsync();

        if (_session is not null)
        {
            try
            {
                await Emulator.StopTraceAsync(_session.SessionId, CancellationToken.None);
            }
            catch { /* best-effort */ }
            finally
            {
                _session = null; // clear reference to ended session
            }
        }

        _isTracing = false;
    }

    /// <summary>
    /// Clears the list of captured transactions.
    /// </summary>
    private void ClearTransactions()
    {
        _transactions.Clear();
        StateHasChanged();
    }

    /// <summary>
    /// Polls the emulator for new trace transactions while the session is active.
    /// </summary>
    /// <param name="ct">Cancellation token for stopping the polling loop.</param>
    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _session is not null)
        {
            try
            {
                var txs = await Emulator.GetTraceTransactionsAsync(_session.SessionId, ct);
                _transactions = [.. txs];
                await InvokeAsync(StateHasChanged);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await InvokeAsync(() =>
                {
                    _error = "Erro no polling: " + ex.Message;
                    StateHasChanged();
                });
                break;
            }

            await Task.Delay(2_000, ct);
        }
    }

    /// <summary>
    /// Cancels the polling loop for trace transactions.
    /// </summary>
    private async Task CancelPollAsync()
    {
        if (_pollCts is not null)
        {
            await _pollCts.CancelAsync();
            _pollCts.Dispose();
            _pollCts = null;
        }

        if (_pollTask is not null)
        {
            try
            {
                await _pollTask; // wait for the loop to actually finish
            }
            catch (OperationCanceledException)
            {
                // expected on cancel
            }
            _pollTask = null;
        }
    }

    /// <summary>
    /// Disposes the component and ensures the trace session is stopped.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await StopTraceInternalAsync();
    }
}