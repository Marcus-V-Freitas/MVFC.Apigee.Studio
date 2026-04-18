namespace MVFC.Apigee.Studio.Blazor.Models;

/// <summary>
/// Represents a toast notification message, including its unique ID, message text, and severity level.
/// </summary>
/// <param name="Id">The unique identifier for the toast message.</param>
/// <param name="Message">The message text to display in the toast.</param>
/// <param name="Level">The severity level of the toast (Success, Error, Warning, Info).</param>
public sealed record ToastMessage(
    Guid Id, 
    string Message, 
    ToastLevel Level);