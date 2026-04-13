namespace ApigeeLocalDev.Blazor.Services;

public enum ToastLevel { Success, Error, Warning, Info }

public sealed record ToastMessage(Guid Id, string Message, ToastLevel Level);

public sealed class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void Success(string msg) => Emit(msg, ToastLevel.Success);
    public void Error(string msg)   => Emit(msg, ToastLevel.Error);
    public void Warning(string msg) => Emit(msg, ToastLevel.Warning);
    public void Info(string msg)    => Emit(msg, ToastLevel.Info);

    private void Emit(string msg, ToastLevel level)
        => OnShow?.Invoke(new ToastMessage(Guid.NewGuid(), msg, level));
}
