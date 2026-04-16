namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class TraceViewer : ComponentBase
{
    [Parameter] 
    public IReadOnlyList<TraceTransaction> Transactions { get; set; } = [];

    [Inject] 
    public required IJSRuntime JSRuntime { get; set; }

    private readonly HashSet<string> _expanded = [];
    private readonly Dictionary<string, TracePoint> _selectedPoints = [];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("initLucide");
        }
        catch
        {
            // Ignore pre-rendering exceptions
        }
    }

    private void Toggle(string messageId)
    {
        if (!_expanded.Add(messageId))
        {
            _expanded.Remove(messageId);
            _selectedPoints.Remove(messageId);
        }
    }

    private void SelectPoint(string messageId, TracePoint pt)
    {
        _selectedPoints[messageId] = pt;
    }

    private void ClearSelection(string messageId)
    {
        _selectedPoints.Remove(messageId);
    }

    private TracePoint? GetSelectedPoint(string messageId)
    {
        return _selectedPoints.TryGetValue(messageId, out var pt) ? pt : null;
    }

    // ─── Grouping Logic ────────────────────────────────────────────────────
    private sealed record PhaseGroup(string PhaseName, List<TracePoint> Points);

    private static List<PhaseGroup> GroupPointsByPhase(IReadOnlyList<TracePoint> points)
    {
        var groups = new List<PhaseGroup>();
        PhaseGroup? current = null;

        foreach (var pt in points)
        {
            if (pt.PointType == "StateChange")
            {
                current = new PhaseGroup(pt.PolicyName, []);
                groups.Add(current);
            }
            else if (current is not null)
            {
                current.Points.Add(pt);
            }
            else
            {
                // Points before any StateChange → fallback group
                current = new PhaseGroup("START", [pt]);
                groups.Add(current);
            }
        }

        // Remove empty groups (StateChange-only phases with no children)
        // Actually keep them to show the phase in the pipeline
        return groups;
    }

    private static string TruncateName(string name)
    {
        if (name.Length <= 22) return name;
        return name[..20] + "…";
    }

    private static string PhaseColorClass(string phase) => phase switch
    {
        "REQ_HEADERS_PARSED" => "phase-clr-proxy-req",
        "PROXY_REQ_FLOW" => "phase-clr-proxy-req",
        "TARGET_REQ_FLOW" => "phase-clr-target-req",
        "REQ_SENT" => "phase-clr-target-req",
        "RESP_START" => "phase-clr-target-resp",
        "TARGET_RESP_FLOW" => "phase-clr-target-resp",
        "PROXY_RESP_FLOW" => "phase-clr-proxy-resp",
        "RESP_SENT" => "phase-clr-proxy-resp",
        "PROXY_POST_RESP_SENT" => "phase-clr-post-resp",
        "END" => "phase-clr-end",
        "DEBUG_SESSION" => "phase-clr-debug",
        _ => "phase-clr-generic"
    };

    private static string PhaseDisplayName(string phase) => phase switch
    {
        "REQ_HEADERS_PARSED" => "Request Received",
        "PROXY_REQ_FLOW" => "Proxy Request",
        "TARGET_REQ_FLOW" => "Target Request",
        "REQ_SENT" => "Request Sent",
        "RESP_START" => "Response Start",
        "TARGET_RESP_FLOW" => "Target Response",
        "PROXY_RESP_FLOW" => "Proxy Response",
        "RESP_SENT" => "Response Sent",
        "PROXY_POST_RESP_SENT" => "PostClient Flow",
        "END" => "End",
        "DEBUG_SESSION" => "Debug Session",
        _ => phase
    };

    private static string PhaseIcon(string phase) => phase switch
    {
        "REQ_HEADERS_PARSED" => "log-in",
        "PROXY_REQ_FLOW" => "arrow-right",
        "TARGET_REQ_FLOW" => "send",
        "REQ_SENT" => "upload",
        "RESP_START" => "download",
        "TARGET_RESP_FLOW" => "inbox",
        "PROXY_RESP_FLOW" => "arrow-left",
        "RESP_SENT" => "check-circle",
        "PROXY_POST_RESP_SENT" => "zap",
        "END" => "flag",
        "DEBUG_SESSION" => "bug",
        _ => "activity"
    };
}