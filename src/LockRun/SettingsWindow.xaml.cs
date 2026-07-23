using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using LockRun.Models;
using LockRun.Services;

namespace LockRun;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        var editableSettings = SettingsLoader.Clone(settings);
        ProcessNamesTextBox.Text = string.Join(Environment.NewLine, editableSettings.MonitoredProcessNames);
        SampleIntervalTextBox.Text = editableSettings.SampleIntervalSeconds.ToString(CultureInfo.InvariantCulture);
        HistoryLimitTextBox.Text = editableSettings.ReportHistoryLimit.ToString(CultureInfo.InvariantCulture);

        LanguageComboBox.SelectedItem = editableSettings.Language switch
        {
            "ja" => JapaneseLanguageItem,
            "en" => EnglishLanguageItem,
            _ => AutoLanguageItem
        };

        ApplyLanguage();
    }

    public AppSettings? ResultSettings { get; private set; }

    private bool UseJapanese
    {
        get
        {
            var language = GetSelectedLanguage();
            return language == "ja" ||
                language == "auto" &&
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja";
        }
    }

    private void LanguageComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (IsInitialized)
        {
            ApplyLanguage();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(
                SampleIntervalTextBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sampleInterval) ||
            sampleInterval is < 2 or > 60)
        {
            ShowValidationError(
                "サンプリング間隔は2～60秒で入力してください。",
                "Enter a sample interval from 2 to 60 seconds.");
            SampleIntervalTextBox.Focus();
            return;
        }

        if (!int.TryParse(
                HistoryLimitTextBox.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var historyLimit) ||
            historyLimit is < 1 or > 100)
        {
            ShowValidationError(
                "保存するレポート数は1～100で入力してください。",
                "Enter a report history limit from 1 to 100.");
            HistoryLimitTextBox.Focus();
            return;
        }

        var processNames = ProcessNamesTextBox.Text
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (processNames.Count == 0)
        {
            ShowValidationError(
                "監視するプロセス名を1つ以上入力してください。",
                "Enter at least one process name to monitor.");
            ProcessNamesTextBox.Focus();
            return;
        }

        ResultSettings = SettingsLoader.Normalize(new AppSettings
        {
            MonitoredProcessNames = processNames,
            Language = GetSelectedLanguage(),
            SampleIntervalSeconds = sampleInterval,
            ReportHistoryLimit = historyLimit
        });
        DialogResult = true;
    }

    private void ApplyLanguage()
    {
        Title = T("設定", "Settings");
        HeadingTextBlock.Text = T("設定", "Settings");
        LanguageLabelTextBlock.Text = T("表示言語", "Language");
        AutoLanguageItem.Content = T("自動（Windowsに合わせる）", "Auto (follow Windows)");
        JapaneseLanguageItem.Content = T("日本語", "Japanese");
        EnglishLanguageItem.Content = T("英語", "English");
        ProcessesLabelTextBlock.Text = T("監視するプロセス名", "Monitored process names");
        ProcessesHelpTextBlock.Text = T(
            "Windowsのプロセス名を1行に1つ入力します。.exeは省略できます。",
            "Enter one Windows process name per line. .exe is optional.");
        SampleIntervalLabelTextBlock.Text = T("サンプリング間隔（秒）", "Sample interval (seconds)");
        SampleIntervalHelpTextBlock.Text = T("入力範囲: 2～60", "Allowed range: 2-60");
        HistoryLimitLabelTextBlock.Text = T("保存するレポート数", "Reports to keep");
        HistoryLimitHelpTextBlock.Text = T("入力範囲: 1～100", "Allowed range: 1-100");
        CancelButton.Content = T("キャンセル", "Cancel");
        SaveButton.Content = T("保存", "Save");
    }

    private string GetSelectedLanguage()
    {
        return (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "auto";
    }

    private string T(string japanese, string english)
    {
        return UseJapanese ? japanese : english;
    }

    private void ShowValidationError(string japanese, string english)
    {
        System.Windows.MessageBox.Show(
            T(japanese, english),
            T("入力内容を確認してください", "Check your settings"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
