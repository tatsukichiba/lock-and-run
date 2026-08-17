using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using LockRun.Models;
using LockRun.Services;

namespace LockRun;

public partial class ProcessPickerWindow : Window
{
    private readonly bool _useJapanese;
    private readonly ObservableCollection<CandidateRow> _candidateRows;
    private readonly ICollectionView _candidateView;

    public ProcessPickerWindow(
        IEnumerable<string> existingProcessNames,
        bool useJapanese)
    {
        InitializeComponent();

        _useJapanese = useJapanese;
        var existingNames = existingProcessNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => ProcessDiscovery.NormalizeProcessName(name.Trim()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _candidateRows = new ObservableCollection<CandidateRow>(
            ProcessDiscovery.Discover()
                .Where(candidate => !existingNames.Contains(candidate.Name))
                .Select(static candidate => new CandidateRow(candidate)));
        _candidateView = CollectionViewSource.GetDefaultView(_candidateRows);
        _candidateView.Filter = MatchesSearch;
        CandidateListView.ItemsSource = _candidateView;

        ApplyLanguage();
        UpdateCandidateCount();
    }

    public IReadOnlyList<string> SelectedProcessNames { get; private set; } = [];

    private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_candidateView is null)
        {
            return;
        }

        _candidateView.Refresh();
        UpdateCandidateCount();
    }

    private bool MatchesSearch(object item)
    {
        if (item is not CandidateRow candidate)
        {
            return false;
        }

        var searchText = SearchTextBox.Text.Trim();
        return searchText.Length == 0 ||
            candidate.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            candidate.MainWindowTitle.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedNames = _candidateRows
            .Where(static candidate => candidate.IsSelected)
            .Select(static candidate => candidate.Name)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedNames.Count == 0)
        {
            System.Windows.MessageBox.Show(
                T("追加するプロセスを1つ以上選択してください。", "Select at least one process to add."),
                T("プロセスを選択", "Select processes"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SelectedProcessNames = selectedNames;
        DialogResult = true;
    }

    private void ApplyLanguage()
    {
        Title = T("実行中のプロセスを追加", "Add running processes");
        HeadingTextBlock.Text = Title;
        DescriptionTextBlock.Text = T(
            "現在実行中のプロセスから、監視対象へ追加するものを選択します。登録済みの名前は表示されません。",
            "Select currently running processes to add to monitoring. Names already configured are hidden.");
        SearchLabelTextBlock.Text = T("検索", "Search");
        NameColumn.Header = T("名前", "Name");
        CountColumn.Header = T("件数", "Count");
        WindowColumn.Header = T("ウィンドウ", "Window");
        CancelButton.Content = T("キャンセル", "Cancel");
        AddButton.Content = T("選択して追加", "Add selected");
    }

    private void UpdateCandidateCount()
    {
        var visibleCount = _candidateView.Cast<object>().Count();
        CandidateCountTextBlock.Text = T(
            $"候補 {visibleCount} / {_candidateRows.Count}",
            $"{visibleCount} / {_candidateRows.Count} candidates");
    }

    private string T(string japanese, string english)
    {
        return _useJapanese ? japanese : english;
    }

    private sealed class CandidateRow(RunningProcessCandidate candidate)
    {
        public bool IsSelected { get; set; }

        public string Name { get; } = candidate.Name;

        public int InstanceCount { get; } = candidate.InstanceCount;

        public string MainWindowTitle { get; } = string.IsNullOrWhiteSpace(candidate.MainWindowTitle)
            ? "-"
            : candidate.MainWindowTitle;
    }
}
