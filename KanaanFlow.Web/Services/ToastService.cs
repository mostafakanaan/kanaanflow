namespace KanaanFlow.Web.Services;

using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class ToastMessage
{
    public ToastMessage(string text, Func<Task>? undo, Func<Task>? share)
    {
        Text = text;
        Undo = undo;
        Share = share;
    }

    public string Text { get; }

    public Func<Task>? Undo { get; }

    public Func<Task>? Share { get; }
}

/// <summary>
/// The short message at the bottom of the screen. Undo lives here instead of "Are you sure?" pop-ups.
/// </summary>
public sealed class ToastService : IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(5);
    private CancellationTokenSource? timer;

    public event Action? Changed;

    public ToastMessage? Current { get; private set; }

    public void Show(string text, Func<Task>? undo = null, Func<Task>? share = null)
    {
        Current = new ToastMessage(text, undo, share);
        Changed?.Invoke();
        RestartTimer();
    }

    public void Clear()
    {
        timer?.Cancel();
        Current = null;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        timer?.Cancel();
        timer?.Dispose();
    }

    private void RestartTimer()
    {
        timer?.Cancel();
        timer?.Dispose();
        CancellationTokenSource source = new CancellationTokenSource();
        timer = source;
        ToastMessage shown = Current!;
        _ = Task.Delay(Lifetime, source.Token).ContinueWith(
            t =>
            {
                if (!t.IsCanceled && ReferenceEquals(Current, shown))
                {
                    Current = null;
                    Changed?.Invoke();
                }
            },
            TaskScheduler.Default);
    }
}
