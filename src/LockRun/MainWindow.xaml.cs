using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LockRun.Models;
using LockRun.Services;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;

namespace LockRun;

public partial class MainWindow : Window
{
    private const string ProductDisplayName = "Lock & Run";
    private static readonly TimeSpan LockConfirmationTimeout = TimeSpan.FromSeconds(15);

    private readonly ObservableCollection<ProcessRow> _processRows = [];
    private readonly WindowsPowerGuard _powerGuard = new();
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _startAwayMenuItem;
    private readonly Forms.ToolStripMenuItem _showMenuItem;
    private readonly Forms.ToolStripMenuItem _exitMenuItem;
    private readonly DispatcherTimer _sampleTimer;
    private readonly DispatcherTimer _lockConfirmationTimer;
    private readonly Icon? _applicationIcon;

    private AppSettings _settings;
    private ProcessMonitor _processMonitor;
    private AwaySession? _currentSession;
    private AwayModeState _state = AwayModeState.Ready;
    private string? _readyStatusMessage;
    private string? _latestReportPath;
    private bool _hasReportContent;
    private int _runningProcessInstanceCount;
    private bool _isExitRequested;

    public MainWindow()
    {
        InitializeComponent();

        _settings = SettingsLoader.Load(out var settingsWarning);
        _processMonitor = new ProcessMonitor(_settings.MonitoredProcessNames);
        ProcessListView.ItemsSource = _processRows;

        _sampleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_settings.SampleIntervalSeconds)
        };
        _sampleTimer.Tick += SampleTimer_Tick;

        _lockConfirmationTimer = new DispatcherTimer
        {
            Interval = LockConfirmationTimeout
        };
        _lockConfirmationTimer.Tick += LockConfirmationTimer_Tick;

        var contextMenu = new Forms.ContextMenuStrip();
        _startAwayMenuItem = new Forms.ToolStripMenuItem();
        _startAwayMenuItem.Click += (_, _) => Dispatcher.BeginInvoke(StartAwayMode);
        _showMenuItem = new Forms.ToolStripMenuItem();
        _showMenuItem.Click += (_, _) => Dispatcher.BeginInvoke(ShowMainWindow);
        _exitMenuItem = new Forms.ToolStripMenuItem();
        _exitMenuItem.Click += (_, _) => Dispatcher.BeginInvoke(ExitApplication);
        contextMenu.Items.AddRange([_startAwayMenuItem, _showMenuItem, _exitMenuItem]);

        _applicationIcon = TryLoadApplicationIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = ProductDisplayName,
            Icon = _applicationIcon ?? SystemIcons.Shield,
            ContextMenuStrip = contextMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(ShowMainWindow);

        SystemEvents.SessionSwitch += OnSessionSwitch;

        ApplyLanguage();
        LoadLatestReport();
        RefreshProcessList();

        if (!string.IsNullOrWhiteSpace(settingsWarning))
        {
            AppendReportWarning(
                T("設定の読み込み時に警告があり、既定値を使用しました。", "Settings could not be fully loaded; defaults were used.") +
                Environment.NewLine + settingsWarning);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExitRequested)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        StopSessionTimers();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        TryDisablePowerGuard(out _);

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _applicationIcon?.Dispose();
        _powerGuard.Dispose();
        base.OnClosing(e);
    }

    public void PrepareForSystemShutdown()
    {
        _isExitRequested = true;
        StopSessionTimers();
        TryDisablePowerGuard(out _);
    }

    public void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Activate();
    }

    private bool UseJapanese =>
        _settings.Language == "ja" ||
        _settings.Language == "auto" &&
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja";

    private void AwayModeButton_Click(object sender, RoutedEventArgs e)
    {
        StartAwayMode();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        TryRefreshProcessList();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_state != AwayModeState.Ready)
        {
            return;
        }

        var settingsWindow = new SettingsWindow(_settings)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() != true || settingsWindow.ResultSettings is null)
        {
            return;
        }

        try
        {
            SettingsLoader.Save(settingsWindow.ResultSettings);
            _settings = settingsWindow.ResultSettings;
            _processMonitor = new ProcessMonitor(_settings.MonitoredProcessNames);
            _sampleTimer.Interval = TimeSpan.FromSeconds(_settings.SampleIntervalSeconds);
            _readyStatusMessage = T("設定を保存しました。", "Settings saved.");
            ApplyLanguage();
            RefreshProcessList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(
                T("設定を保存できませんでした。", "Could not save settings.") + Environment.NewLine + ex.Message,
                ProductDisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenReportsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ReportStore.ReportsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = ReportStore.ReportsDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            System.Windows.MessageBox.Show(
                T("レポートフォルダーを開けませんでした。", "Could not open the report folder.") +
                Environment.NewLine + ex.Message,
                ProductDisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void StartAwayMode()
    {
        if (_state != AwayModeState.Ready)
        {
            return;
        }

        try
        {
            var runningProcesses = _processMonitor.GetRunningProcesses();
            var appResourceAtStart = AppResourceSnapshot.CaptureCurrentProcess();
            var startedAt = DateTimeOffset.Now;

            _powerGuard.Enable();
            _currentSession = new AwaySession(startedAt, runningProcesses, appResourceAtStart);
            _readyStatusMessage = null;
            SetState(AwayModeState.AwaitingLock);
            RefreshProcessList(runningProcesses);

            _lockConfirmationTimer.Stop();
            _lockConfirmationTimer.Start();
            _notifyIcon.ShowBalloonTip(
                3000,
                ProductDisplayName,
                T(
                    "スリープ防止を有効化しました。Windowsをロックしています。",
                    "Sleep prevention is on. Windows is locking now."),
                Forms.ToolTipIcon.Info);

            WindowsPowerGuard.LockWorkStation();
        }
        catch (Exception ex)
        {
            AbortSession(
                T("監視の開始に失敗しました。", "Could not start monitoring."),
                ex.Message);
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        Dispatcher.BeginInvoke(() => HandleSessionSwitch(e.Reason));
    }

    private void HandleSessionSwitch(SessionSwitchReason reason)
    {
        if (reason == SessionSwitchReason.SessionLock &&
            _state == AwayModeState.AwaitingLock &&
            _currentSession is not null)
        {
            _lockConfirmationTimer.Stop();
            _currentSession.MarkLockConfirmed(DateTimeOffset.Now);
            TryCaptureSample();
            _sampleTimer.Start();
            SetState(AwayModeState.Monitoring);
            return;
        }

        if (reason == SessionSwitchReason.SessionUnlock)
        {
            if (_state == AwayModeState.Monitoring)
            {
                CompleteAwayMode();
            }
            else if (_state == AwayModeState.AwaitingLock)
            {
                AbortSession(
                    T("Windowsのロックを確認できませんでした。", "Windows lock could not be confirmed."),
                    null);
            }
        }
    }

    private void LockConfirmationTimer_Tick(object? sender, EventArgs e)
    {
        _lockConfirmationTimer.Stop();
        if (_state != AwayModeState.AwaitingLock)
        {
            return;
        }

        AbortSession(
            T(
                "15秒以内にWindowsのロックを確認できなかったため、スリープ防止を解除しました。",
                "Windows lock was not confirmed within 15 seconds, so sleep prevention was turned off."),
            null);
    }

    private void SampleTimer_Tick(object? sender, EventArgs e)
    {
        if (_state != AwayModeState.Monitoring)
        {
            return;
        }

        TryCaptureSample();
    }

    private void TryCaptureSample()
    {
        if (_currentSession is null)
        {
            return;
        }

        try
        {
            var runningProcesses = _processMonitor.GetRunningProcesses();
            _currentSession.AddSample(DateTimeOffset.Now, runningProcesses);
            RefreshProcessList(runningProcesses);
        }
        catch
        {
            _currentSession.RegisterSamplingError();
            UpdateUiState();
        }
    }

    private void CompleteAwayMode()
    {
        if (_currentSession is null || _state != AwayModeState.Monitoring)
        {
            return;
        }

        var completedSession = _currentSession;
        AwayReport? report = null;
        var disableError = default(string);

        SetState(AwayModeState.Completing);
        StopSessionTimers();
        TryDisablePowerGuard(out disableError);

        try
        {
            var runningProcesses = _processMonitor.GetRunningProcesses();
            var appResourceAtReturn = AppResourceSnapshot.CaptureCurrentProcess();
            report = AwayReport.Create(completedSession, runningProcesses, appResourceAtReturn);
            var summary = report.ToSummary(UseJapanese);

            try
            {
                _latestReportPath = ReportStore.SaveSummary(
                    report.StartedAt,
                    summary,
                    _settings.ReportHistoryLimit,
                    out var cleanupWarning);

                if (!string.IsNullOrWhiteSpace(cleanupWarning))
                {
                    summary += Environment.NewLine + Environment.NewLine +
                        T("古いレポートの整理に失敗しました: ", "Could not trim old reports: ") +
                        cleanupWarning;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                summary += Environment.NewLine + Environment.NewLine +
                    T("レポートを保存できませんでした: ", "Could not save the report: ") + ex.Message;
            }

            if (!string.IsNullOrWhiteSpace(disableError))
            {
                summary += Environment.NewLine + Environment.NewLine +
                    T("スリープ防止の解除エラー: ", "Sleep prevention cleanup error: ") + disableError;
            }

            _hasReportContent = true;
            ReportTextBox.Text = summary;
            RefreshProcessList(runningProcesses);
            _readyStatusMessage = T(
                "離席セッションが完了しました。スリープ防止はオフです。",
                "Away session completed. Sleep prevention is off.");
        }
        catch (Exception ex)
        {
            _hasReportContent = true;
            ReportTextBox.Text = T(
                "離席セッションは終了しましたが、レポート作成に失敗しました。",
                "The away session ended, but report generation failed.") + Environment.NewLine + ex.Message;
            _readyStatusMessage = T(
                "レポートエラーが発生しました。スリープ防止はオフです。",
                "A report error occurred. Sleep prevention is off.");
        }
        finally
        {
            _currentSession = null;
            SetState(AwayModeState.Ready);
            ShowMainWindow();
        }

        if (report is not null)
        {
            _notifyIcon.ShowBalloonTip(
                5000,
                ProductDisplayName,
                T(
                    $"離席終了。継続: {report.StillRunningCount}、終了: {report.EndedCount}、新規: {report.NewCount}",
                    $"Away session ended. Running: {report.StillRunningCount}, ended: {report.EndedCount}, new: {report.NewCount}."),
                Forms.ToolTipIcon.Info);
        }
    }

    private void AbortSession(string message, string? details)
    {
        StopSessionTimers();
        TryDisablePowerGuard(out var disableError);
        _currentSession = null;

        var reportMessage = message;
        if (!string.IsNullOrWhiteSpace(details))
        {
            reportMessage += Environment.NewLine + details;
        }

        if (!string.IsNullOrWhiteSpace(disableError))
        {
            reportMessage += Environment.NewLine +
                T("スリープ防止の解除エラー: ", "Sleep prevention cleanup error: ") + disableError;
        }

        _hasReportContent = true;
        ReportTextBox.Text = reportMessage;
        _readyStatusMessage = message;
        SetState(AwayModeState.Ready);
        ShowMainWindow();
        _notifyIcon.ShowBalloonTip(
            5000,
            ProductDisplayName,
            message,
            Forms.ToolTipIcon.Error);
    }

    private void TryRefreshProcessList()
    {
        try
        {
            RefreshProcessList();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                T("プロセス一覧を更新できませんでした。", "Could not refresh the process list.") +
                Environment.NewLine + ex.Message,
                ProductDisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RefreshProcessList()
    {
        RefreshProcessList(_processMonitor.GetRunningProcesses());
    }

    private void RefreshProcessList(IReadOnlyList<MonitoredProcessInfo> processes)
    {
        _processRows.Clear();
        foreach (var processGroup in processes
                     .GroupBy(static process => process.Name, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            _processRows.Add(new ProcessRow(processGroup));
        }

        _runningProcessInstanceCount = processes.Count;

        ConfiguredProcessesTextBlock.Text = T(
            $"監視設定: {string.Join(", ", _processMonitor.ProcessNames)}",
            $"Configured: {string.Join(", ", _processMonitor.ProcessNames)}");
        UpdateUiState();
    }

    private void SetState(AwayModeState state)
    {
        _state = state;
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        var processTypeCount = _processRows.Count;
        var ready = _state == AwayModeState.Ready;

        AwayModeButton.IsEnabled = ready;
        SettingsButton.IsEnabled = ready;
        RefreshButton.IsEnabled = _state is AwayModeState.Ready or AwayModeState.Monitoring;
        _startAwayMenuItem.Enabled = ready;

        switch (_state)
        {
            case AwayModeState.Ready:
                SetStateIndicator(T("準備完了", "Ready"), 232, 245, 238, 22, 121, 74);
                StatusTextBlock.Text = _readyStatusMessage ?? T(
                    $"準備完了。現在 {processTypeCount} 種類 / {_runningProcessInstanceCount} プロセスが稼働しています。",
                    $"Ready. {processTypeCount} type(s) / {_runningProcessInstanceCount} monitored process(es) are running.");
                AwayModeButton.Content = T("監視を開始してロック", "Monitor and lock");
                _notifyIcon.Text = ProductDisplayName;
                break;

            case AwayModeState.AwaitingLock:
                SetStateIndicator(T("ロック待ち", "Locking"), 255, 244, 218, 151, 89, 0);
                StatusTextBlock.Text = T(
                    "Windowsのロック確認を待っています。スリープ防止はオンです。",
                    "Waiting for Windows lock confirmation. Sleep prevention is on.");
                AwayModeButton.Content = T("ロック確認中", "Confirming lock");
                _notifyIcon.Text = T("Lock & Run - ロック確認中", "Lock & Run - Locking");
                break;

            case AwayModeState.Monitoring:
                SetStateIndicator(T("監視中", "Monitoring"), 229, 240, 255, 29, 95, 170);
                StatusTextBlock.Text = T(
                    $"監視中。{_currentSession?.Samples.Count ?? 0} 回記録し、{processTypeCount} 種類 / {_runningProcessInstanceCount} プロセスが稼働中です。",
                    $"Monitoring. {_currentSession?.Samples.Count ?? 0} samples; {processTypeCount} type(s) / {_runningProcessInstanceCount} process(es) running.");
                AwayModeButton.Content = T("監視中", "Monitoring");
                _notifyIcon.Text = T("Lock & Run - 監視中", "Lock & Run - Monitoring");
                break;

            case AwayModeState.Completing:
                SetStateIndicator(T("集計中", "Finishing"), 235, 238, 243, 77, 88, 105);
                StatusTextBlock.Text = T("復帰レポートを作成しています。", "Creating the return report.");
                AwayModeButton.Content = T("集計中", "Finishing");
                _notifyIcon.Text = ProductDisplayName;
                break;
        }
    }

    private void SetStateIndicator(
        string text,
        byte backgroundRed,
        byte backgroundGreen,
        byte backgroundBlue,
        byte foregroundRed,
        byte foregroundGreen,
        byte foregroundBlue)
    {
        StateIndicatorTextBlock.Text = text;
        StateIndicatorBorder.Background = new SolidColorBrush(MediaColor.FromRgb(
            backgroundRed,
            backgroundGreen,
            backgroundBlue));
        StateIndicatorTextBlock.Foreground = new SolidColorBrush(MediaColor.FromRgb(
            foregroundRed,
            foregroundGreen,
            foregroundBlue));
    }

    private void ApplyLanguage()
    {
        Title = ProductDisplayName;
        TitleTextBlock.Text = ProductDisplayName;
        SubtitleTextBlock.Text = T(
            "Windowsをロックしている間も、長時間処理を起こしたまま活動を記録します。",
            "Keep long-running jobs awake and record activity while Windows is locked.");
        RefreshButton.Content = T("更新", "Refresh");
        SettingsButton.Content = T("設定", "Settings");
        ProcessListHeadingTextBlock.Text = T("現在稼働中の監視対象", "Monitored processes currently running");
        LatestReportHeadingTextBlock.Text = T("最新レポート", "Latest report");
        OpenReportsButton.Content = T("フォルダーを開く", "Open folder");
        FooterTextBlock.Text = T(
            "ウィンドウを閉じてもトレイで動作します。終了はトレイメニューから行えます。",
            "Closing the window keeps Lock & Run in the tray. Use the tray menu to exit.");
        NameColumn.Header = T("名前", "Name");
        CountColumn.Header = T("件数", "Count");
        PidColumn.Header = "PIDs";
        WindowColumn.Header = T("ウィンドウ", "Window");

        _startAwayMenuItem.Text = T("監視を開始してロック", "Monitor and lock");
        _showMenuItem.Text = T("表示", "Show");
        _exitMenuItem.Text = T("終了", "Exit");

        if (!_hasReportContent)
        {
            ReportTextBox.Text = T(
                "完了した離席セッションはまだありません。",
                "No away session has completed yet.");
        }

        UpdateUiState();
    }

    private void LoadLatestReport()
    {
        if (ReportStore.TryLoadLatest(out var summary, out var reportPath, out var warning))
        {
            _latestReportPath = reportPath;
            _hasReportContent = true;
            ReportTextBox.Text = summary;
        }
        else if (!string.IsNullOrWhiteSpace(warning))
        {
            AppendReportWarning(
                T("前回レポートを読み込めませんでした: ", "Could not load the previous report: ") + warning);
        }
    }

    private void AppendReportWarning(string warning)
    {
        if (_hasReportContent)
        {
            ReportTextBox.Text += Environment.NewLine + Environment.NewLine + warning;
        }
        else
        {
            _hasReportContent = true;
            ReportTextBox.Text = warning;
        }
    }

    private void StopSessionTimers()
    {
        _lockConfirmationTimer.Stop();
        _sampleTimer.Stop();
    }

    private bool TryDisablePowerGuard(out string? error)
    {
        error = null;
        if (!_powerGuard.IsEnabled)
        {
            return true;
        }

        try
        {
            _powerGuard.Disable();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void ExitApplication()
    {
        if (_state != AwayModeState.Ready)
        {
            var result = Forms.MessageBox.Show(
                T(
                    "監視を終了してLock & Runを終了しますか？",
                    "Stop monitoring and exit Lock & Run?"),
                ProductDisplayName,
                Forms.MessageBoxButtons.YesNo,
                Forms.MessageBoxIcon.Warning);

            if (result != Forms.DialogResult.Yes)
            {
                return;
            }
        }

        _isExitRequested = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private static Icon? TryLoadApplicationIcon()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        try
        {
            return System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string T(string japanese, string english)
    {
        return UseJapanese ? japanese : english;
    }

    private sealed class ProcessRow
    {
        public ProcessRow(IEnumerable<MonitoredProcessInfo> processes)
        {
            var instances = processes.OrderBy(static process => process.ProcessId).ToList();
            Name = instances[0].Name;
            InstanceCount = instances.Count;
            ProcessIds = string.Join(", ", instances
                .Take(5)
                .Select(static process => process.ProcessId));
            if (instances.Count > 5)
            {
                ProcessIds += $" +{instances.Count - 5}";
            }

            var windowTitles = instances
                .Select(static process => process.MainWindowTitle)
                .Where(static title => !string.IsNullOrWhiteSpace(title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();
            MainWindowTitle = windowTitles.Count == 0
                ? "-"
                : string.Join(" / ", windowTitles);
        }

        public string Name { get; }

        public int InstanceCount { get; }

        public string ProcessIds { get; }

        public string MainWindowTitle { get; }
    }
}
