namespace MVFC.Apigee.Studio.Blazor.Components.Layout;

/// <summary>
/// Main layout component for the Blazor application.
/// Provides a shared layout structure and exposes the <see cref="ToastService"/> for displaying toast notifications.
/// </summary>
public partial class MainLayout : LayoutComponentBase
{
    /// <summary>
    /// Service for displaying toast notifications in the application.
    /// </summary>
    [Inject]
    public required ToastService Toast { get; set; }
}