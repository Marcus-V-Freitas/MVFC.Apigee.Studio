namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

/// <summary>
/// Blazor component for visualizing trace transactions and their execution points in a grouped, expandable view.
/// Supports selection, grouping by phase, and integration with JavaScript for icon rendering.
/// </summary>
public partial class TraceViewer : ComponentBase
{
    /// <summary>
    /// List of trace transactions to display.
    /// </summary>
    [Parameter]
    public IReadOnlyList<TraceTransaction> Transactions { get; set; } = [];

    /// <summary>
    /// JavaScript runtime for invoking JS interop (e.g., icon initialization).
    /// </summary>
    [Inject]
    public required IJSRuntime JSRuntime { get; set; }

    /// <summary>
    /// Set of expanded transaction message IDs.
    /// </summary>
    private readonly HashSet<string> _expanded = [];

    /// <summary>
    /// Dictionary of selected trace points per transaction message ID.
    /// </summary>
    private readonly Dictionary<string, TracePoint> _selectedPoints = [];

    /// <summary>
    /// Returns a CSS class representing the HTTP response status code category.
    /// "status-5xx" for server errors (500+), "status-4xx" for client errors (400–499),
    /// and "status-2xx" for successful or other responses.
    /// </summary>
    /// <param name="responseCode">The HTTP response status code.</param>
    /// <returns>A CSS class string for styling the status badge.</returns>
    private static string GetStatusClass(int responseCode)
    {
        if (responseCode >= 500 || responseCode == 0)
            return "status-5xx";
        if (responseCode >= 400)
            return "status-4xx";
        return "status-2xx";
    }

    /// <summary>
    /// Initializes Lucide icons after the component is rendered.
    /// </summary>
    /// <param name="firstRender">Indicates if this is the first render.</param>
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

    /// <summary>
    /// Toggles the expanded/collapsed state of a transaction by message ID.
    /// </summary>
    /// <param name="messageId">The message ID of the transaction.</param>
    private void Toggle(string messageId)
    {
        if (!_expanded.Add(messageId))
        {
            _expanded.Remove(messageId);
            _selectedPoints.Remove(messageId);
        }
    }

    /// <summary>
    /// Selects a trace point for a given transaction message ID.
    /// </summary>
    /// <param name="messageId">The message ID of the transaction.</param>
    /// <param name="pt">The trace point to select.</param>
    private void SelectPoint(string messageId, TracePoint pt)
    {
        _selectedPoints[messageId] = pt;
    }

    /// <summary>
    /// Clears the selected trace point for a given transaction message ID.
    /// </summary>
    /// <param name="messageId">The message ID of the transaction.</param>
    private void ClearSelection(string messageId)
    {
        _selectedPoints.Remove(messageId);
    }

    /// <summary>
    /// Gets the selected trace point for a given transaction message ID, or null if none is selected.
    /// </summary>
    /// <param name="messageId">The message ID of the transaction.</param>
    /// <returns>The selected <see cref="TracePoint"/> or null.</returns>
    private TracePoint? GetSelectedPoint(string messageId)
    {
        return _selectedPoints.TryGetValue(messageId, out var pt) ? pt : null;
    }

    // ─── Grouping Logic ────────────────────────────────────────────────────

    /// <summary>
    /// Represents a group of trace points belonging to a specific phase.
    /// </summary>
    private sealed record PhaseGroup(string PhaseName, List<TracePoint> Points);

    /// <summary>
    /// Groups trace points by phase, using "StateChange" points as phase delimiters.
    /// Example: Each "StateChange" starts a new phase group.
    /// </summary>
    /// <param name="points">The list of trace points to group.</param>
    /// <returns>A list of <see cref="PhaseGroup"/> objects.</returns>
    private static List<PhaseGroup> GroupPointsByPhase(IReadOnlyList<TracePoint> points)
    {
        var groups = new List<PhaseGroup>();
        PhaseGroup? current = null;

        foreach (var pt in points)
        {
            if (string.Equals(pt.PointType, "StateChange", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Truncates a name to a maximum of 22 characters, adding an ellipsis if needed.
    /// </summary>
    /// <param name="name">The name to truncate.</param>
    /// <returns>The truncated name.</returns>
    private static string TruncateName(string name)
    {
        if (name.Length <= 22) return name;
        return name[..20] + "…";
    }

    /// <summary>
    /// Returns a CSS class for the given phase, used for coloring the UI.
    /// </summary>
    /// <param name="phase">The phase name.</param>
    /// <returns>A CSS class string.</returns>
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
        _ => "phase-clr-generic",
    };

    /// <summary>
    /// Returns a display name for the given phase.
    /// </summary>
    /// <param name="phase">The phase name.</param>
    /// <returns>A user-friendly display name.</returns>
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
        _ => phase,
    };

    /// <summary>
    /// Returns an icon name for the given phase, used for UI display.
    /// </summary>
    /// <param name="phase">The phase name.</param>
    /// <returns>The icon name as a string.</returns>
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
        _ => "activity",
    };
}