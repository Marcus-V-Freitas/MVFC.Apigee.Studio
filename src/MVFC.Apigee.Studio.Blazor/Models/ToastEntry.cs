namespace MVFC.Apigee.Studio.Blazor.Models;

/// <summary>
/// Represents a toast notification entry, including its unique ID, message, level, and exit state.
/// </summary>
/// <param name="Id">The unique identifier for the toast entry.</param>
/// <param name="Message">The message to display in the toast.</param>
/// <param name="Level">The severity level of the toast (Success, Error, Warning, Info).</param>
public sealed class ToastEntry(Guid Id, string Message, ToastLevel Level)
{
    /// <summary>
    /// The unique identifier for the toast entry.
    /// </summary>
    public Guid Id { get; } = Id;

    /// <summary>
    /// The message to display in the toast.
    /// </summary>
    public string Message { get; } = Message;

    /// <summary>
    /// The severity level of the toast.
    /// </summary>
    public ToastLevel Level { get; } = Level;
    
    /// <summary>
    /// Indicates whether the toast is in the process of exiting (for animation or removal).
    /// </summary>
    public bool Exiting { get; set; }
}