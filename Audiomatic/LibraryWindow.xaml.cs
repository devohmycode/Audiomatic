using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Audiomatic;

public sealed partial class LibraryWindow : Window
{
    private IReadOnlyList<LibraryRow> _allRows = [];
    private LibrarySortColumn _sortColumn = LibrarySortColumn.Title;
    private bool _sortAscending = true;

    public LibraryWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(CustomTitleBar);
        WindowShadow.Apply(this);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
        }

        ApplyTheme(SettingsManager.LoadTheme());
        UpdateHeaderTexts();
    }

    public void SetRows(IReadOnlyList<LibraryRow> rows)
    {
        _allRows = rows;
        ApplySort();
    }

    private void ApplyTheme(string theme)
    {
        if (Content is not FrameworkElement root) return;

        root.RequestedTheme = theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void TitleHeader_Click(object sender, RoutedEventArgs e) =>
        ChangeSort(LibrarySortColumn.Title);

    private void FolderHeader_Click(object sender, RoutedEventArgs e) =>
        ChangeSort(LibrarySortColumn.Folder);

    private void ChangeSort(LibrarySortColumn column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        ApplySort();
    }

    private void ApplySort()
    {
        IEnumerable<LibraryRow> sorted = _sortColumn switch
        {
            LibrarySortColumn.Folder => _sortAscending
                ? _allRows.OrderBy(r => r.FolderPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                : _allRows.OrderByDescending(r => r.FolderPath, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(r => r.Title, StringComparer.OrdinalIgnoreCase),
            _ => _sortAscending
                ? _allRows.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.FolderPath, StringComparer.OrdinalIgnoreCase)
                : _allRows.OrderByDescending(r => r.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(r => r.FolderPath, StringComparer.OrdinalIgnoreCase)
        };

        RowsList.ItemsSource = sorted.ToList();
        EmptyStateText.Visibility = _allRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateHeaderTexts();
    }

    private void UpdateHeaderTexts()
    {
        TitleHeaderText.Text = BuildHeader("Titre", _sortColumn == LibrarySortColumn.Title);
        FolderHeaderText.Text = BuildHeader("Dossier", _sortColumn == LibrarySortColumn.Folder);
    }

    private string BuildHeader(string label, bool isActive)
    {
        if (!isActive) return label;
        return _sortAscending ? $"{label} ▲" : $"{label} ▼";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public sealed record LibraryRow(string Title, string FolderPath);

internal enum LibrarySortColumn
{
    Title,
    Folder
}
