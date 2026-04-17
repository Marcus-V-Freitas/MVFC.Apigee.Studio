namespace MVFC.Apigee.Studio.Blazor.Services;

public sealed class EditorStateService
{
    private readonly List<EditorTab> _openTabs = [];
    private EditorTab? _activeTab;

    public IReadOnlyList<EditorTab> OpenTabs => _openTabs;
    public EditorTab? ActiveTab => _activeTab;

    public event Action? OnTabsChanged;

    public void OpenTab(string path, string content)
    {
        var existing = _openTabs.FirstOrDefault(t => t.FullPath == path);
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

    public void SwitchToTab(EditorTab tab)
    {
        if (_openTabs.Contains(tab))
        {
            _activeTab = tab;
            OnTabsChanged?.Invoke();
        }
    }

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

    public void UpdateActiveTabContent(string content, bool isDirty)
    {
        if (_activeTab is not null)
        {
            _activeTab.Content = content;
            _activeTab.IsDirty = isDirty;
            OnTabsChanged?.Invoke();
        }
    }

    public void ClearDirty(string path)
    {
        var tab = _openTabs.FirstOrDefault(t => t.FullPath == path);
        if (tab is not null)
        {
            tab.IsDirty = false;
            OnTabsChanged?.Invoke();
        }
    }

    public void Reset()
    {
        _openTabs.Clear();
        _activeTab = null;
        OnTabsChanged?.Invoke();
    }
}
