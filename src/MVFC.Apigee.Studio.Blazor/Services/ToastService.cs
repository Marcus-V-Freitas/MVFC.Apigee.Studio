namespace MVFC.Apigee.Studio.Blazor.Services;

/// <summary>
/// Service for displaying toast notifications in the Blazor application.
/// Provides methods to show success, error, warning, and info messages.
/// </summary>
public sealed class ToastService
{
    /// <summary>
    /// Event triggered when a toast message should be shown.
    /// </summary>
    public event Action<ToastMessage>? OnShow;

    /// <summary>
    /// Shows a success toast notification with the specified message.
    /// </summary>
    /// <param name="msg">The message to display.</param>
    public void ShowSuccess(string msg) =>
        Emit(msg, ToastLevel.Success);

    /// <summary>
    /// Shows an error toast notification with the specified message.
    /// </summary>
    /// <param name="msg">The message to display.</param>
    public void ShowError(string msg) =>
        Emit(msg, ToastLevel.Error);

    /// <summary>
    /// Shows a warning toast notification with the specified message.
    /// </summary>
    /// <param name="msg">The message to display.</param>
    public void ShowWarning(string msg) =>
        Emit(msg, ToastLevel.Warning);

    /// <summary>
    /// Shows an informational toast notification with the specified message.
    /// </summary>
    /// <param name="msg">The message to display.</param>
    public void ShowInfo(string msg) =>
        Emit(msg, ToastLevel.Info);

    /// <summary>
    /// Emits a toast message with the specified content and level.
    /// </summary>
    /// <param name="msg">The message to display.</param>
    /// <param name="level">The toast level (success, error, warning, info).</param>
    private void Emit(string msg, ToastLevel level)
        => OnShow?.Invoke(new ToastMessage(Guid.NewGuid(), msg, level));
}
