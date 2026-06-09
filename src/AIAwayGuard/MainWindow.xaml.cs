using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using AIAwayGuard.Models;
using AIAwayGuard.Services;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace AIAwayGuard;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProcessRow> _processRows = [];
    private readonly ProcessMonitor _processMonitor;
    private readonly WindowsPowerGuard _powerGuard = new();
    private readonly Forms.NotifyIcon _notifyIcon;
    private AwaySession? _currentSession;
    private bool _isExitRequested;

    public MainWindow()
    {
        InitializeComponent();

        var settings = SettingsLoader.Load();
        _processMonitor = new ProcessMonitor(settings.MonitoredProcessNames);
        ProcessListView.ItemsSource = _processRows;
        ConfiguredProcessesTextBlock.Text = $"Configured: {string.Join(", ", _processMonitor.ProcessNames)}";

        _notifyIcon = CreateNotifyIcon();
        _notifyIcon.Visible = true;

        SystemEvents.SessionSwitch += OnSessionSwitch;
        RefreshProcessList();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExitRequested)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        SystemEvents.SessionSwitch -= OnSessionSwitch;
        TryDisablePowerGuard();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.OnClosing(e);
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add("Away Mode Start", null, (_, _) => StartAwayMode());
        contextMenu.Items.Add("Show", null, (_, _) => ShowMainWindow());
        contextMenu.Items.Add("Exit", null, (_, _) => ExitApplication());

        var notifyIcon = new Forms.NotifyIcon
        {
            Text = "AI Away Guard",
            Icon = SystemIcons.Shield,
            ContextMenuStrip = contextMenu
        };
        notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
        return notifyIcon;
    }

    private void AwayModeButton_Click(object sender, RoutedEventArgs e)
    {
        StartAwayMode();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProcessList();
    }

    private void StartAwayMode()
    {
        try
        {
            var runningProcesses = _processMonitor.GetRunningProcesses();
            _powerGuard.Enable();
            _currentSession = new AwaySession(DateTimeOffset.Now, runningProcesses);
            RefreshProcessList(runningProcesses);

            StatusTextBlock.Text = $"Away mode active since {_currentSession.StartedAt:HH:mm:ss}. Windows is locking now.";
            _notifyIcon.ShowBalloonTip(
                3000,
                "AI Away Guard",
                "Sleep prevention is on. Windows is locking now.",
                Forms.ToolTipIcon.Info);

            WindowsPowerGuard.LockWorkStation();
        }
        catch (Exception ex)
        {
            _currentSession = null;
            TryDisablePowerGuard();
            RefreshProcessList();
            StatusTextBlock.Text = "Away mode failed.";
            Forms.MessageBox.Show(
                ex.Message,
                "AI Away Guard",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Error);
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            Dispatcher.Invoke(CompleteAwayMode);
        }
    }

    private void CompleteAwayMode()
    {
        if (_currentSession is null)
        {
            RefreshProcessList();
            return;
        }

        try
        {
            var runningProcesses = _processMonitor.GetRunningProcesses();
            var report = AwayReport.Create(_currentSession, runningProcesses);
            _currentSession = null;

            RefreshProcessList(runningProcesses);
            ReportTextBox.Text = report.ToSummary();
            StatusTextBlock.Text = "Away mode completed. Sleep prevention is off.";
            ShowMainWindow();
            _notifyIcon.ShowBalloonTip(
                5000,
                "AI Away Guard",
                $"Away session ended. Still running: {report.StillRunningCount}, ended: {report.EndedCount}.",
                Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _currentSession = null;
            ReportTextBox.Text = $"Away session ended, but report generation failed: {ex.Message}";
            StatusTextBlock.Text = "Away mode completed with a report error. Sleep prevention is off.";
            ShowMainWindow();
        }
        finally
        {
            TryDisablePowerGuard();
        }
    }

    private void RefreshProcessList()
    {
        RefreshProcessList(_processMonitor.GetRunningProcesses());
    }

    private void RefreshProcessList(IReadOnlyList<MonitoredProcessInfo> processes)
    {
        _processRows.Clear();
        foreach (var process in processes)
        {
            _processRows.Add(new ProcessRow(process));
        }

        StatusTextBlock.Text = _powerGuard.IsEnabled
            ? $"Sleep prevention is on. Monitoring {processes.Count} running process(es)."
            : $"Ready. Monitoring {processes.Count} running process(es).";
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _isExitRequested = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void TryDisablePowerGuard()
    {
        if (!_powerGuard.IsEnabled)
        {
            return;
        }

        try
        {
            _powerGuard.Disable();
        }
        catch (Exception ex)
        {
            ReportTextBox.Text = $"Sleep prevention disable failed: {ex.Message}";
        }
    }

    private sealed class ProcessRow
    {
        public ProcessRow(MonitoredProcessInfo process)
        {
            Name = process.Name;
            ProcessId = process.ProcessId;
            StartedAtText = process.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unavailable";
            MainWindowTitle = string.IsNullOrWhiteSpace(process.MainWindowTitle)
                ? "-"
                : process.MainWindowTitle;
        }

        public string Name { get; }

        public int ProcessId { get; }

        public string StartedAtText { get; }

        public string MainWindowTitle { get; }
    }
}
