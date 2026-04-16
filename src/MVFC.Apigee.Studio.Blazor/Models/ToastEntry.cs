namespace MVFC.Apigee.Studio.Blazor.Models;

public sealed class ToastEntry(Guid Id, string Message, ToastLevel Level)
{
    public Guid Id { get; } = Id;

    public string Message { get; } = Message;

    public ToastLevel Level { get; } = Level;
    
    public bool Exiting { get; set; }
}