using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Keyce.Models;
using Keyce.Services;

namespace Keyce.Controls;

/// <summary>Hôte d'éditeur multi-documents intégrable dans la fenêtre principale.</summary>
public partial class EditorTabs : UserControl, INotifyPropertyChanged
{
    private static readonly string RecentWorkspacesFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Keyce Code", "recent-workspaces.json");
    private IEditorService? _editorService;
    private bool _synchronizingSelection;
    private EditorDocument? _selectedDocument;
    private bool _hasDocuments;
    private string _toolchainWelcome = "Exécutez vos programmes avec F5";

    public EditorTabs()
    {
        InitializeComponent();
        DataContext = this;
        LoadRecentWorkspaces();
    }

    public ObservableCollection<EditorDocument> Documents { get; } = [];
    public ObservableCollection<RecentWorkspace> RecentWorkspaces { get; } = [];

    public bool HasDocuments
    {
        get => _hasDocuments;
        private set
        {
            if (_hasDocuments == value) return;
            _hasDocuments = value;
            OnPropertyChanged();
        }
    }

    public string ToolchainWelcome
    {
        get => _toolchainWelcome;
        set
        {
            if (_toolchainWelcome == value) return;
            _toolchainWelcome = value;
            OnPropertyChanged();
        }
    }

    public event Action<string>? RecentWorkspaceRequested;

    public void AddRecentWorkspace(string path)
    {
        if (!Directory.Exists(path)) return;
        var normalizedPath = Path.GetFullPath(path);
        var existing = RecentWorkspaces.FirstOrDefault(item =>
            string.Equals(item.Path, normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) RecentWorkspaces.Remove(existing);
        RecentWorkspaces.Insert(0, new RecentWorkspace(Path.GetFileName(normalizedPath), normalizedPath));
        while (RecentWorkspaces.Count > 8) RecentWorkspaces.RemoveAt(RecentWorkspaces.Count - 1);
        SaveRecentWorkspaces();
    }

    public EditorDocument? SelectedDocument
    {
        get => _selectedDocument;
        private set
        {
            if (ReferenceEquals(_selectedDocument, value))
                return;
            _selectedDocument = value;
            OnPropertyChanged();
            Editor.LoadDocument(value);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? TextChanged;
    public event EventHandler? CaretPositionChanged;
    public event EventHandler? ActiveDocumentChanged;

    public int CaretLine => Editor.CaretLine;
    public int CaretColumn => Editor.CaretColumn;

    public string CurrentLanguage => Path.GetExtension(_editorService?.ActiveDocument?.FilePath ?? string.Empty).ToLowerInvariant() switch
    {
        ".cs" => "C#",
        ".py" => "Python",
        ".js" => "JavaScript",
        ".java" => "Java",
        ".c" => "C",
        ".cpp" or ".cc" or ".cxx" => "C++",
        _ => "Texte brut"
    };

    /// <summary>Connecte le contrôle à l'implémentation partagée du service éditeur.</summary>
    public void Initialize(IEditorService editorService)
    {
        ArgumentNullException.ThrowIfNull(editorService);
        if (_editorService is not null)
            _editorService.ActiveDocumentChanged -= EditorService_ActiveDocumentChanged;

        _editorService = editorService;
        _editorService.ActiveDocumentChanged += EditorService_ActiveDocumentChanged;
        Editor.CaretPositionChanged += (_, _) => CaretPositionChanged?.Invoke(this, EventArgs.Empty);
        SynchronizeDocuments();
    }

    /// <summary>Charge un fichier dans un onglet, en activant l'onglet si déjà ouvert.</summary>
    public async Task LoadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureService();
        await _editorService!.OpenAsync(path, cancellationToken);
    }

    public string GetCurrentText() => Editor.GetText();

    public void OpenAtLine(int lineNumber) => Editor.GoToLine(lineNumber);

    public EditorDocument NewDocument()
    {
        EnsureService();
        return _editorService!.NewDocument();
    }

    public async Task SaveCurrentAsync(CancellationToken cancellationToken = default)
    {
        EnsureService();
        if (_editorService!.ActiveDocument is not { } document)
            return;

        var path = document.FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Enregistrer le fichier",
                Filter = "Tous les fichiers (*.*)|*.*|Fichiers C# (*.cs)|*.cs",
                DefaultExt = ".cs",
                AddExtension = true,
                FileName = "Sans titre.cs"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;
            path = dialog.FileName;
        }

        await _editorService.SaveAsync(document, path, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SaveAllAsync(CancellationToken cancellationToken = default)
    {
        EnsureService();
        var errors = new List<string>();
        var previous = _editorService!.ActiveDocument;
        try
        {
            foreach (var document in _editorService.OpenDocuments.Where(document => document.IsDirty).ToArray())
            {
                Activate(document);
                try
                {
                    await SaveCurrentAsync(cancellationToken);
                    if (document.IsDirty)
                        errors.Add($"Enregistrement annulé : {document.DisplayName}");
                }
                catch (Exception exception)
                {
                    errors.Add($"{document.DisplayName} : {exception.Message}");
                }
            }
        }
        finally
        {
            if (previous is not null && _editorService.OpenDocuments.Contains(previous))
                Activate(previous);
        }

        return errors;
    }

    public async Task<bool> CloseDocumentAsync(EditorDocument document, CancellationToken cancellationToken = default)
    {
        EnsureService();
        if (document.IsDirty)
        {
            var answer = MessageBox.Show(Window.GetWindow(this),
                $"Enregistrer les modifications apportées à « {document.DisplayName} » ?",
                "Keyce Code", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Cancel)
                return false;
            if (answer == MessageBoxResult.Yes)
            {
                var previous = _editorService!.ActiveDocument;
                Activate(document);
                await SaveCurrentAsync(cancellationToken);
                if (document.IsDirty)
                {
                    if (previous is not null)
                        Activate(previous);
                    return false;
                }
                if (previous is not null && !ReferenceEquals(previous, document))
                    Activate(previous);
            }
        }

        _editorService!.Close(document);
        return true;
    }

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is EditorDocument document)
        {
            try { await CloseDocumentAsync(document); }
            catch (Exception exception) { ShowEditorError("Impossible d'enregistrer le fichier", exception); }
        }
        e.Handled = true;
    }

    private void TabsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_synchronizingSelection || TabsList.SelectedItem is not EditorDocument document || _editorService is null)
            return;
        Activate(document);
    }

    private void EditorService_ActiveDocumentChanged(object? sender, EventArgs e) => SynchronizeDocuments();

    private void SynchronizeDocuments()
    {
        if (_editorService is null)
            return;

        _synchronizingSelection = true;
        try
        {
            var openDocuments = _editorService.OpenDocuments;
            foreach (var removed in Documents.Where(document => !openDocuments.Contains(document)).ToArray())
                Documents.Remove(removed);
            for (var index = 0; index < openDocuments.Count; index++)
            {
                var document = openDocuments[index];
                var existingIndex = Documents.IndexOf(document);
                if (existingIndex < 0)
                    Documents.Insert(index, document);
                else if (existingIndex != index)
                {
                    Documents.Move(existingIndex, index);
                }
            }

            SelectedDocument = _editorService.ActiveDocument;
            HasDocuments = Documents.Count > 0;
            TabsList.SelectedItem = SelectedDocument;
            ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _synchronizingSelection = false;
        }
    }

    private void RecentWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is RecentWorkspace workspace)
            RecentWorkspaceRequested?.Invoke(workspace.Path);
    }

    private void LoadRecentWorkspaces()
    {
        try
        {
            if (!File.Exists(RecentWorkspacesFile)) return;
            var items = JsonSerializer.Deserialize<List<RecentWorkspace>>(File.ReadAllText(RecentWorkspacesFile));
            if (items is null) return;
            foreach (var item in items.Where(item => Directory.Exists(item.Path)).Take(8))
                RecentWorkspaces.Add(item);
            if (items.Count != RecentWorkspaces.Count) SaveRecentWorkspaces();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Un historique indisponible ne doit pas empêcher le démarrage de l'éditeur.
        }
    }

    private void SaveRecentWorkspaces()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RecentWorkspacesFile)!);
            File.WriteAllText(RecentWorkspacesFile, JsonSerializer.Serialize(RecentWorkspaces.ToArray()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // L'historique reste facultatif si le profil utilisateur n'est pas accessible.
        }
    }

    private void Activate(EditorDocument document)
    {
        if (_editorService is EditorService implementation)
            implementation.Activate(document);
        else if (document.FilePath is not null)
            _ = _editorService!.OpenAsync(document.FilePath);
        else
            throw new InvalidOperationException("Le service éditeur doit permettre l'activation d'un document sans chemin.");
    }

    private void Editor_TextChanged(object sender, EventArgs e)
    {
        if (_editorService?.ActiveDocument is { } document)
            _editorService.UpdateContent(document, Editor.GetText());
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EditorTabs_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            NewDocument();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            SaveFromShortcut();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W && SelectedDocument is { } document)
        {
            CloseDocumentFromShortcutAsync(document);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            Editor.ShowFind();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.G)
        {
            Editor.PromptForLine();
            e.Handled = true;
        }
    }

    private async void SaveFromShortcut()
    {
        try { await SaveCurrentAsync(); }
        catch (Exception exception) { ShowEditorError("Impossible d'enregistrer le fichier", exception); }
    }

    private async void CloseDocumentFromShortcutAsync(EditorDocument document)
    {
        try { await CloseDocumentAsync(document); }
        catch (Exception exception) { ShowEditorError("Impossible d'enregistrer le fichier", exception); }
    }

    private void EnsureService()
    {
        if (_editorService is null)
            throw new InvalidOperationException("Appelez Initialize avec un IEditorService avant d'utiliser les onglets.");
    }

    private void ShowEditorError(string title, Exception exception) =>
        MessageBox.Show(Window.GetWindow(this), exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record RecentWorkspace(string Name, string Path);
