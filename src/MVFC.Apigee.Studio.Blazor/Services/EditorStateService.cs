namespace MVFC.Apigee.Studio.Blazor.Services;

/// <summary>
/// Service for managing the state of editor tabs in the Blazor application.
/// Handles opening, switching, closing, and updating tabs, as well as tracking the active tab and dirty state.
/// </summary>
public sealed class EditorStateService
{
    /// <summary>
    /// List of currently open editor tabs.
    /// </summary>
    private readonly List<EditorTab> _openTabs = [];

    /// <summary>
    /// The currently active editor tab.
    /// </summary>
    private EditorTab? _activeTab;

    /// <summary>
    /// Gets a read-only list of open editor tabs.
    /// </summary>
    public IReadOnlyList<EditorTab> OpenTabs => _openTabs;

    /// <summary>
    /// Gets the currently active editor tab, or null if none is active.
    /// </summary>
    public EditorTab? ActiveTab => _activeTab;

    /// <summary>
    /// Event triggered whenever the set of open tabs or the active tab changes.
    /// </summary>
    public event Action? OnTabsChanged;

    /// <summary>
    /// Opens a tab for the specified file path and content.
    /// If the tab is already open, it becomes the active tab; otherwise, a new tab is created and activated.
    /// </summary>
    /// <param name="path">The full file path to open.</param>
    /// <param name="content">The file content to display in the tab.</param>
    public void OpenTab(string path, string content)
    {
        var existing = _openTabs.FirstOrDefault(t => string.Equals(t.FullPath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _activeTab = existing;
        }
        else
        {
            var newTab = new EditorTab { FullPath = path, Content = content };
            _openTabs.Add(newTab);
            _activeTab = newTab;
        }
        OnTabsChanged?.Invoke();
    }

    /// <summary>
    /// Switches the active tab to the specified tab, if it is open.
    /// </summary>
    /// <param name="tab">The tab to activate.</param>
    public void SwitchToTab(EditorTab tab)
    {
        if (_openTabs.Contains(tab))
        {
            _activeTab = tab;
            OnTabsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Closes the specified tab. If the closed tab was active, activates the next available tab.
    /// </summary>
    /// <param name="tab">The tab to close.</param>
    public void CloseTab(EditorTab tab)
    {
        var index = _openTabs.IndexOf(tab);
        if (index == -1) return;

        _openTabs.RemoveAt(index);

        if (_activeTab == tab)
        {
            _activeTab = _openTabs.Count > 0
                ? _openTabs[Math.Min(index, _openTabs.Count - 1)]
                : null;
        }

        OnTabsChanged?.Invoke();
    }

    /// <summary>
    /// Updates the content and dirty state of the active tab.
    /// </summary>
    /// <param name="content">The new content for the tab.</param>
    /// <param name="isDirty">Indicates whether the tab has unsaved changes.</param>
    public void UpdateActiveTabContent(string content, bool isDirty)
    {
        if (_activeTab is not null)
        {
            _activeTab.Content = content;
            _activeTab.IsDirty = isDirty;
            OnTabsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Clears the dirty state of the tab with the specified file path.
    /// </summary>
    /// <param name="path">The full file path of the tab to update.</param>
    public void ClearDirty(string path)
    {
        var tab = _openTabs.FirstOrDefault(t => string.Equals(t.FullPath, path, StringComparison.OrdinalIgnoreCase));
        if (tab is not null)
        {
            tab.IsDirty = false;
            OnTabsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Closes all tabs except the specified one.
    /// </summary>
    /// <param name="keep">The tab to keep open.</param>
    public void CloseOtherTabs(EditorTab keep)
    {
        var toRemove = _openTabs.Where(t => t != keep).ToList();
        foreach (var tab in toRemove)
        {
            _openTabs.Remove(tab);
        }

        _activeTab = keep;

        OnTabsChanged?.Invoke();
    }

    /// <summary>
    /// Closes all open tabs.
    /// </summary>
    public void CloseAllTabs()
    {
        Reset();
    }

    /// <summary>
    /// Closes all tabs and resets the active tab.
    /// </summary>
    public void Reset()
    {
        _openTabs.Clear();
        _activeTab = null;
        OnTabsChanged?.Invoke();
    }
}
