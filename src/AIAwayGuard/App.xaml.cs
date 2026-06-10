using System.Threading;
using System.Windows;

namespace AIAwayGuard;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\AIAwayGuard.SingleInstance";
    private const string ShowMainWindowEventName = @"Local\AIAwayGuard.ShowMainWindow";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showMainWindowEvent;
    private RegisteredWaitHandle? _showMainWindowRegistration;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;

            TrySignalExistingInstance();
            Shutdown();
            return;
        }

        _showMainWindowEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            ShowMainWindowEventName);
        _showMainWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showMainWindowEvent,
            OnShowMainWindowRequested,
            null,
            Timeout.Infinite,
            false);

        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showMainWindowRegistration?.Unregister(null);
        _showMainWindowRegistration = null;

        _showMainWindowEvent?.Dispose();
        _showMainWindowEvent = null;

        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }

    private static void TrySignalExistingInstance()
    {
        try
        {
            using var showMainWindowEvent = EventWaitHandle.OpenExisting(ShowMainWindowEventName);
            showMainWindowEvent.Set();
        }
        catch (Exception ex) when (
            ex is WaitHandleCannotBeOpenedException ||
            ex is UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(
                "AI Away Guard is already running, but the existing window could not be requested.",
                "AI Away Guard",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnShowMainWindowRequested(object? state, bool timedOut)
    {
        if (timedOut)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.ShowMainWindow();
            }
        });
    }
}
