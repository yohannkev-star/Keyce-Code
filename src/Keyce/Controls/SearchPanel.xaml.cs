using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Keyce.Controls;

public partial class SearchPanel : UserControl
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "bin", "obj", "node_modules", "publish"
    };

    private CancellationTokenSource? _searchCancellation;

    public SearchPanel() => InitializeComponent();

    public string? RootPath { get; set; }
    public event Action<string, int>? ResultActivated;

    public void FocusQuery()
    {
        QueryBox.Focus();
        QueryBox.SelectAll();
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync();

    private async void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        var token = _searchCancellation.Token;
        ResultsList.ItemsSource = null;

        if (string.IsNullOrWhiteSpace(RootPath) || !Directory.Exists(RootPath))
        {
            SearchStatus.Text = "Ouvre un dossier pour rechercher dans son contenu.";
            return;
        }

        var query = QueryBox.Text.Trim();
        if (query.Length == 0)
        {
            SearchStatus.Text = "Saisis un texte à rechercher.";
            return;
        }

        SearchStatus.Text = "Recherche en cours…";
        try
        {
            var results = await Task.Run(() => FindMatches(RootPath, query, MatchCaseBox.IsChecked == true, token), token);
            if (token.IsCancellationRequested) return;
            ResultsList.ItemsSource = results;
            SearchStatus.Text = results.Count == 0
                ? "Aucun résultat."
                : $"{results.Count} occurrence(s) trouvée(s). Double-clique sur un résultat pour l’ouvrir.";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SearchStatus.Text = $"Recherche interrompue : {exception.Message}";
        }
    }

    private static List<SearchResult> FindMatches(string rootPath, string query, bool matchCase, CancellationToken token)
    {
        var results = new List<SearchResult>();
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0 && results.Count < 500)
        {
            token.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            try
            {
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if (!IgnoredDirectories.Contains(Path.GetFileName(child))) pending.Push(child);
                }

                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    token.ThrowIfCancellationRequested();
                    var info = new FileInfo(file);
                    if (info.Length > 1_000_000) continue;
                    try
                    {
                        var lineNumber = 0;
                        foreach (var line in File.ReadLines(file))
                        {
                            lineNumber++;
                            if (!line.Contains(query, comparison)) continue;
                            results.Add(new SearchResult(file, Path.GetFileName(file), lineNumber,
                                line.Trim(), $"{Path.GetRelativePath(rootPath, file)}:{lineNumber}"));
                            if (results.Count >= 500) break;
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
                    {
                        // Ignore files that are locked or are not readable as text.
                    }
                    if (results.Count >= 500) break;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Ignore folders that cannot be enumerated.
            }
        }

        return results;
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is SearchResult result)
            ResultActivated?.Invoke(result.FilePath, result.LineNumber);
    }

    private sealed record SearchResult(string FilePath, string FileName, int LineNumber, string Preview, string Location);
}
