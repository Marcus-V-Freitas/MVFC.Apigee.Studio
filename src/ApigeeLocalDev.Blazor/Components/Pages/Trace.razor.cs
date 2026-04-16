namespace ApigeeLocalDev.Blazor.Components.Pages;

public partial class Trace : ComponentBase, IAsyncDisposable
{
    private string _activeTab = "trace";
    private string _proxyName = string.Empty;
    private bool _isTracing = false;
    private bool _loading = false;
    private string? _error = null;
    private string? _selectedWorkspacePath;

    private TraceSession? _session = null;
    private CancellationTokenSource? _pollCts = null;
 
    private IReadOnlyList<TraceTransaction> _transactions = [];    
    private IReadOnlyList<ApigeeWorkspace> _workspaces = [];
    private IReadOnlyList<string> _proxies = [];

    [Inject]
    public required IApigeeEmulatorClient Emulator { get; set; }

    [Inject]
    public required IWorkspaceRepository WorkspaceRepo { get; set; }
    
    [Inject]
    public required ToastService Toast { get; set; }

    protected override void OnInitialized() => 
        _workspaces = WorkspaceRepo.ListAll();

    private void OnWorkspaceChanged(ChangeEventArgs e)
    {
        _selectedWorkspacePath = e.Value?.ToString();
        _proxyName = string.Empty;
        _proxies = [];

        var ws = _workspaces.FirstOrDefault(w => w.RootPath == _selectedWorkspacePath);
        if (ws is not null)
        {
            _proxies = WorkspaceRepo.ListApiProxies(ws);
        }
    }

    private void OnProxyChanged(ChangeEventArgs e) => 
        _proxyName = e.Value?.ToString() ?? string.Empty;

    private bool IsWorkspaceNotReady =>
        string.IsNullOrEmpty(_selectedWorkspacePath) || _isTracing;

    private async Task StartTrace()
    {
        _error = null;
        _loading = true;
        StateHasChanged();

        try
        {
            _session = await Emulator.StartTraceAsync(_proxyName);
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

    private async Task StopTrace()
    {
        await CancelPollAsync();

        if (_session is not null)
        {
            try 
            { 
                await Emulator.StopTraceAsync(_session.SessionId); 
            }
            catch { /* best-effort */ }
        }

        _isTracing = false;
        Toast.ShowSuccess("Trace encerrado.");
    }

    private void ClearTransactions()
    {
        _transactions = [];
        StateHasChanged();
    }

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

            await Task.Delay(2_000, ct).ConfigureAwait(false);
        }
    }

    private async Task CancelPollAsync()
    {
        if (_pollCts is not null)
        {
            await _pollCts.CancelAsync();
            _pollCts.Dispose();
            _pollCts = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await CancelPollAsync();

        if (_isTracing && _session is not null)
        {
            try 
            { 
                await Emulator.StopTraceAsync(_session.SessionId); 
            }
            catch { /* best-effort */ }
        }
    }
}