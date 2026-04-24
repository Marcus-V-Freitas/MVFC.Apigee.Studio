namespace MVFC.Apigee.Studio.Blazor.Services;

/// <summary>
/// Scoped service for persisting page state across navigations within a Blazor Server circuit.
/// Each page uses a unique key prefix to store/retrieve its state.
/// </summary>
public sealed class SessionStateService
{
    private readonly Dictionary<string, object?> _store = [];

    /// <summary>
    /// Event triggered when the session state changes.
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Sets a value in the session state.
    /// </summary>
    public void Set<T>(string key, T value)
    {
        _store[key] = value;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Gets a value from the session state if it exists and matches the type.
    /// </summary>
    public T? Get<T>(string key) =>
        _store.TryGetValue(key, out var value) && value is T typed ? typed : default;

    /// <summary>
    /// Checks if a key exists in the session state.
    /// </summary>
    public bool Has(string key) => _store.ContainsKey(key);

    /// <summary>
    /// Removes a key from the session state.
    /// </summary>
    public void Remove(string key) => _store.Remove(key);
}
