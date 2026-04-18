namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

/// <summary>
/// Blazor page component for tracing API proxy transactions using the Apigee Emulator.
/// Allows the user to select a workspace and proxy, start and stop trace sessions, and view captured transactions in real time.
/// </summary>
public partial class Trace : ComponentBase, IAsyncDisposable
{
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
    /// Stores the last error message encountered during trace operations.
    /// </summary>
    private string? _error;

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
    private IReadOnlyList<TraceTransaction> _transactions = [];

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
    /// Starts a trace session for the selected proxy and begins polling for transactions.
    /// </summary>
    private async Task StartTrace()
    {
        _error = null;
        _loading = true;
        StateHasChanged();

        try
        {
            _session = await Emulator.StartTraceAsync(_proxyName, _pollCts?.Token ?? CancellationToken.None);
            _isTracing = true;
            _transactions = [];

            _pollCts = new CancellationTokenSource();
            _ = PollLoopAsync(_pollCts.Token);

            Toast.ShowSuccess($"Trace iniciado para '{_proxyName}'");
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            Toast.ShowError("Falha ao iniciar trace: " + ex.Message);
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
                await Emulator.StopTraceAsync(_session.SessionId, _pollCts?.Token ?? CancellationToken.None);
            }
            catch { /* best-effort */ }
        }

        _isTracing = false;
    }

    /// <summary>
    /// Clears the list of captured transactions.
    /// </summary>
    private void ClearTransactions()
    {
        _transactions = [];
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

                if (txs.Count != _transactions.Count)
                {
                    _transactions = [.. txs];
                    await InvokeAsync(StateHasChanged);
                }
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
    }

    /// <summary>
    /// Disposes the component and ensures the trace session is stopped.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await StopTraceInternalAsync();
    }
}