namespace ApigeeLocalDev.Blazor.Models;

public sealed record ToastMessage(
    Guid Id, 
    string Message, 
    ToastLevel Level);