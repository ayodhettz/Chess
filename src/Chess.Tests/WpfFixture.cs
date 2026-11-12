using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using ChessUI;

namespace Chess.Tests;

public sealed class WpfFixture : IDisposable
{
    private readonly Thread thread;
    private readonly Dispatcher dispatcher;

    public WpfFixture()
    {
        using ManualResetEventSlim ready = new();
        Dispatcher? startedDispatcher = null;
        Exception? startupError = null;
        thread = new Thread(() =>
        {
            try
            {
                App app = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.InitializeComponent();
                startedDispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception error)
            {
                startupError = error;
            }
            finally
            {
                ready.Set();
            }
            if (startupError == null) Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("WPF startup timed out.");
        if (startupError != null) ExceptionDispatchInfo.Capture(startupError).Throw();
        dispatcher = startedDispatcher!;
    }

    public void Run(Action action) => dispatcher.Invoke(action);

    public void Dispose()
    {
        dispatcher.InvokeShutdown();
        thread.Join(TimeSpan.FromSeconds(10));
    }
}
