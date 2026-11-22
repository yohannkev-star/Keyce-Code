using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using Microsoft.Win32;
using Keyce.Controls;
using Keyce.Models;
using Keyce.Services;
using Keyce.ViewModels;
using Keyce.Views;

namespace Keyce;

/// <summary>
/// Fenêtre principale (Développeur 1).
/// Elle ne contient AUCUNE logique métier : elle expose des événements (menus, raccourcis)
/// et des méthodes pour que les autres modules s'y branchent à l'intégration (H24-H30).
/// </summary>
public partial class MainWindow : Window
{
    private readonly FileService _fileService;
    private readonly EditorService _editorService;
    private readonly EditorTabs _editorTabs;
    private readonly ExplorerViewModel _explorerViewModel;
    private readonly ProjectService _projectService;
    private readonly IRunnerService _runnerService;
    private readonly IToolchainDetector _toolchainDetector;
    private readonly OutputPanel _outputPanel;
    private string? _lastRunDirectory;

    // ---------- Événements à brancher par l'intégration ----------
    public event EventHandler? NewFileRequested;      // Dev 3 / Dev 2
    public event EventHandler? OpenFileRequested;     // Dev 3
    public event EventHandler? OpenFolderRequested;   // Dev 3
    public event EventHandler? NewProjectRequested;   // Dev 3
    public event EventHandler? SaveRequested;         // Dev 3
    public event EventHandler? SaveAllRequested;      // Dev 3
    public event EventHandler? CloseTabRequested;     // Dev 2
    public event EventHandler? FindRequested;         // Dev 2
    public event EventHandler? GoToLineRequested;     // Dev 2
    public event EventHandler? RunRequested;          // Dev 4
    public event EventHandler? StopRequested;         // Dev 4
    public event EventHandler? ClearOutputRequested;  // Dev 4

    private double _explorerWidth = 250;
    private double _panelHeight = 220;
    private bool _explorerVisible = true;
    private bool _panelVisible = true;

    public MainWindow()
    {
        InitializeComponent();
        RegisterCommands();

        _fileService = new FileService();
        _editorService = new EditorService(_fileService);
        _editorTabs = new EditorTabs();
        _explorerViewModel = new ExplorerViewModel();
        _projectService = new ProjectService();
        _runnerService = new ProcessRunner();
        _toolchainDetector = new ToolchainDetector();
        _outputPanel = new OutputPanel();

        _editorTabs.ActiveDocumentChanged += (_, _) => UpdateEditorStatus();
        _editorTabs.CaretPositionChanged += (_, _) =>
            SetCaretPosition(_editorTabs.CaretLine, _editorTabs.CaretColumn);
        _editorTabs.Initialize(_editorService);
        _editorTabs.RecentWorkspaceRequested += OpenRecentFolder;
        SetEditorContent(_editorTabs);

        var explorer = new ExplorerPanel { DataContext = _explorerViewModel };
        SetExplorerContent(explorer);
        _explorerViewModel.FileOpenRequested += path => _ = OpenEditorFileAsync(path);

        SetOutputContent(_outputPanel);
        _runnerService.OutputReceived += _outputPanel.OnOutputReceived;
        _outputPanel.StopRequested += _runnerService.Stop;
        _outputPanel.ErrorClicked += OpenErrorLocation;

        NewFileRequested += (_, _) => _editorTabs.NewDocument();
        OpenFileRequested += async (_, _) => await OpenFileAsync();
        OpenFolderRequested += (_, _) => OpenFolder();
        SaveRequested += async (_, _) => await SaveEditorAsync();
        SaveAllRequested += async (_, _) => await SaveAllEditorsAsync();
        CloseTabRequested += async (_, _) => await CloseSelectedDocumentAsync();
        FindRequested += (_, _) => _editorTabs.Editor.ShowFind();
        GoToLineRequested += (_, _) => _editorTabs.Editor.PromptForLine();
        RunRequested += async (_, _) => await RunCurrentAsync();
        StopRequested += (_, _) => _runnerService.Stop();
        ClearOutputRequested += (_, _) => _outputPanel.Clear();

        UpdateEditorStatus();
        _ = DetectToolchainAsync();
    }

    private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Ouvrir un fichier",
            Filter = "Tous les fichiers (*.*)|*.*|Fichiers C# (*.cs)|*.cs",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            await OpenEditorFileAsync(dialog.FileName);
    }

    private async Task<bool> OpenEditorFileAsync(string path)
    {
        try
        {
            await _editorTabs.LoadFileAsync(path);
            SetStatusMessage($"Ouvert : {path}");
            UpdateEditorStatus();
            return true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Impossible d'ouvrir le fichier",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void OpenFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Ouvrir un dossier de projet" };
        if (dialog.ShowDialog(this) != true)
            return;

        OpenFolderPath(dialog.FolderName);
    }

    private void OpenFolderPath(string path)
    {
        _explorerViewModel.OpenFolder(path);
        _editorTabs.AddRecentWorkspace(path);
        SetStatusMessage($"Dossier ouvert : {path}");
    }

    private void OpenRecentFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            SetStatusMessage("Ce dossier récent n’existe plus.");
            return;
        }

        OpenFolderPath(path);
    }

    private void CommandCenter_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var input = CommandCenter.Text.Trim().Trim('"');
        CommandCenter.Clear();
        if (string.IsNullOrWhiteSpace(input)) return;

        switch (input.ToLowerInvariant())
        {
            case "nouveau":
            case "new":
                NewFileRequested?.Invoke(this, EventArgs.Empty);
                return;
            case "ouvrir":
            case "open":
                OpenFileRequested?.Invoke(this, EventArgs.Empty);
                return;
            case "dossier":
            case "folder":
                OpenFolderRequested?.Invoke(this, EventArgs.Empty);
                return;
            case "exécuter":
            case "executer":
            case "run":
                RunRequested?.Invoke(this, EventArgs.Empty);
                return;
            case "explorateur":
            case "explorer":
                if (!_explorerVisible) ToggleExplorer();
                return;
            case "sortie":
            case "terminal":
            case "output":
                ShowOutputPanel();
                return;
        }

        if (File.Exists(input))
            _ = OpenEditorFileAsync(Path.GetFullPath(input));
        else if (Directory.Exists(input))
            OpenFolderPath(Path.GetFullPath(input));
        else
            SetStatusMessage($"Aucun fichier, dossier ou commande trouvé pour « {input} ».");
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P)
        {
            CommandCenter.Focus();
            CommandCenter.SelectAll();
            e.Handled = true;
        }
    }

    private async Task SaveEditorAsync()
    {
        try
        {
            await _editorTabs.SaveCurrentAsync();
            SetStatusMessage("Fichier enregistré");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Impossible d'enregistrer le fichier",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveAllEditorsAsync()
    {
        var errors = await _editorTabs.SaveAllAsync();
        if (errors.Count > 0)
            MessageBox.Show(this, string.Join(Environment.NewLine, errors), "Certains fichiers n'ont pas été enregistrés",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            SetStatusMessage("Tous les fichiers sont enregistrés");
    }

    private async Task CloseSelectedDocumentAsync()
    {
        if (_editorTabs.SelectedDocument is { } document)
            await _editorTabs.CloseDocumentAsync(document);
    }

    private void UpdateEditorStatus()
    {
        SetLanguage(_editorTabs.CurrentLanguage);
        SetCaretPosition(_editorTabs.CaretLine, _editorTabs.CaretColumn);
    }

    private async Task DetectToolchainAsync()
    {
        try
        {
            var tools = await _toolchainDetector.DetectAsync();
            var dotnet = tools.FirstOrDefault(tool => tool.Name.Equals("dotnet", StringComparison.OrdinalIgnoreCase));
            SetToolchain(dotnet is null ? "Aucun SDK .NET détecté" : $"SDK .NET {dotnet.Version}");
        }
        catch (Exception exception)
        {
            SetToolchain("Détection SDK impossible");
            SetStatusMessage(exception.Message);
        }
    }

    private async Task RunCurrentAsync()
    {
        if (_runnerService.IsRunning)
        {
            SetStatusMessage("Une exécution est déjà en cours.");
            return;
        }

        var document = _editorTabs.SelectedDocument;
        if (document is not null)
        {
            if (document.FilePath is null || document.IsDirty)
                await SaveEditorAsync();

            if (document.FilePath is null || document.IsDirty)
            {
                SetStatusMessage("Enregistre le fichier avant de l'exécuter.");
                return;
            }
        }

        var target = ResolveExecutionTarget(document);
        if (target is null)
        {
            SetStatusMessage("Ouvre un projet ou un fichier pris en charge avant d'appuyer sur F5.");
            return;
        }

        _lastRunDirectory = Directory.Exists(target)
            ? target
            : Path.GetDirectoryName(target);
        _outputPanel.Clear();
        ShowOutputPanel();
        SetStatusMessage("Exécution en cours...");

        try
        {
            var result = await _runnerService.RunAsync(target);
            var summary = result.WasCancelled
                ? "Exécution arrêtée."
                : result.Success
                    ? $"Terminé avec le code {result.ExitCode} ({result.Duration.TotalSeconds:F1} s)."
                    : $"Échec avec le code {result.ExitCode} ({result.Duration.TotalSeconds:F1} s).";
            _outputPanel.Append(summary, result.Success ? OutputKind.Success : OutputKind.Info);
            SetStatusMessage(summary);
        }
        catch (Exception exception)
        {
            _outputPanel.AppendLine(exception.Message, true);
            SetStatusMessage("Échec de l'exécution");
        }
    }

    private string? ResolveExecutionTarget(EditorDocument? document)
    {
        if (document?.FilePath is { } filePath)
        {
            var extension = Path.GetExtension(filePath);
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var projectDirectory = FindProjectDirectory(Path.GetDirectoryName(filePath));
                if (projectDirectory is null)
                {
                    MessageBox.Show(this,
                        "Aucun projet .NET (.csproj) trouvé dans ce dossier ou ses dossiers parents. Ouvre un projet Console C# pour exécuter ce fichier.",
                        "Projet .NET introuvable", MessageBoxButton.OK, MessageBoxImage.Information);
                    return null;
                }
                return projectDirectory;
            }

            return filePath;
        }

        if (!string.IsNullOrWhiteSpace(_explorerViewModel.RootPath))
            return _explorerViewModel.RootPath;

        return null;
    }

    private string? FindProjectDirectory(string? startDirectory)
    {
        var directory = startDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (_projectService.FindProjectFile(directory) is not null)
                return directory;

            var parent = Directory.GetParent(directory)?.FullName;
            if (parent is null || string.Equals(parent, directory, StringComparison.OrdinalIgnoreCase))
                break;
            directory = parent;
        }

        return null;
    }

    private async void OpenErrorLocation(string file, int line)
    {
        var fullPath = Path.IsPathRooted(file)
            ? file
            : Path.GetFullPath(Path.Combine(_lastRunDirectory ?? Environment.CurrentDirectory, file));

        if (await OpenEditorFileAsync(fullPath))
            _editorTabs.OpenAtLine(line);
    }

    // ---------- Commandes ----------
    private void Register(RoutedCommand command, Func<MainWindow, EventHandler?> Get)
    {
        CommandBindings.Add(new CommandBinding(command, (_, _) => Get(this)?.Invoke(this, EventArgs.Empty)));
    }

    private void RegisterCommands()
    {
        Register(ApplicationCommands.New, w => w.NewFileRequested);
        Register(ApplicationCommands.Open, w => w.OpenFileRequested);
        Register(ApplicationCommands.Save, w => w.SaveRequested);
        Register(ApplicationCommands.Find, w => w.FindRequested);
        Register(AppCommands.OpenFolder, w => w.OpenFolderRequested);
        Register(AppCommands.NewProject, w => w.NewProjectRequested);
        Register(AppCommands.SaveAll, w => w.SaveAllRequested);
        Register(AppCommands.CloseTab, w => w.CloseTabRequested);
        Register(AppCommands.GoToLine, w => w.GoToLineRequested);
        Register(AppCommands.Run, w => w.RunRequested);
        Register(AppCommands.Stop, w => w.StopRequested);
        Register(AppCommands.ClearOutput, w => w.ClearOutputRequested);

        // Ces deux-là sont gérés par la fenêtre elle-même
        CommandBindings.Add(new CommandBinding(AppCommands.ToggleExplorer, (_, _) => ToggleExplorer()));
        CommandBindings.Add(new CommandBinding(AppCommands.TogglePanel, (_, _) => TogglePanel()));
    }

    // ---------- Affichage / masquage des zones ----------
    public void ToggleExplorer()
    {
        if (_explorerVisible)
        {
            _explorerWidth = Math.Max(150, ExplorerColumn.ActualWidth);
            ExplorerColumn.MinWidth = 0;
            ExplorerColumn.Width = new GridLength(0);
            ExplorerPanel.Visibility = Visibility.Collapsed;
            ExplorerSplitter.Visibility = Visibility.Collapsed;
        }
        else
        {
            ExplorerColumn.MinWidth = 150;
            ExplorerColumn.Width = new GridLength(_explorerWidth);
            ExplorerPanel.Visibility = Visibility.Visible;
            ExplorerSplitter.Visibility = Visibility.Visible;
        }
        _explorerVisible = !_explorerVisible;
        ExplorerButton.IsChecked = _explorerVisible;
    }

    public void TogglePanel()
    {
        if (_panelVisible) HidePanel(); else ShowPanel();
    }

    public void ShowPanel()
    {
        if (_panelVisible) return;
        PanelRow.MinHeight = 80;
        PanelRow.Height = new GridLength(_panelHeight);
        BottomPanel.Visibility = Visibility.Visible;
        PanelSplitter.Visibility = Visibility.Visible;
        _panelVisible = true;
    }

    public void HidePanel()
    {
        if (!_panelVisible) return;
        _panelHeight = Math.Max(80, PanelRow.ActualHeight);
        PanelRow.MinHeight = 0;
        PanelRow.Height = new GridLength(0);
        BottomPanel.Visibility = Visibility.Collapsed;
        PanelSplitter.Visibility = Visibility.Collapsed;
        _panelVisible = false;
    }

    // ---------- API d'intégration : brancher les modules des autres devs ----------
    public void SetExplorerContent(UIElement content) => ExplorerHost.Content = content;   // Dev 3
    public void SetEditorContent(UIElement content) => EditorHost.Content = content;       // Dev 2
    public void SetOutputContent(UIElement content) => OutputHost.Content = content;       // Dev 4
    public void SetProblemsContent(UIElement content) => ProblemsHost.Content = content;   // Dev 4

    /// <summary>Affiche le panneau bas sur l'onglet SORTIE (appelé au lancement de F5).</summary>
    public void ShowOutputPanel()
    {
        ShowPanel();
        OutputTab.IsChecked = true;
    }

    // ---------- API barre d'état ----------
    public void SetStatusMessage(string text) => StatusMessage.Text = text;
    public void SetCaretPosition(int line, int column) => StatusPosition.Content = $"Ln {line}, Col {column}";
    public void SetLanguage(string language) => StatusLanguage.Content = language;
    public void SetEncoding(string encoding) => StatusEncoding.Content = encoding;
    /// <summary>Ex. "SDK .NET 8.0.100" ou "Aucun SDK détecté" (fourni par ToolchainDetector, Dev 4).</summary>
    public void SetToolchain(string text)
    {
        StatusToolchain.Content = text;
        _editorTabs.ToolchainWelcome = text;
    }

    // ---------- Gestionnaires d'événements XAML ----------
    private void ExplorerButton_Click(object sender, RoutedEventArgs e) => ToggleExplorer();

    private void PanelTab_Checked(object sender, RoutedEventArgs e)
    {
        // Appelé aussi pendant InitializeComponent : on vérifie que tout existe
        if (OutputHost == null || ProblemsHost == null) return;
        bool output = OutputTab.IsChecked == true;
        OutputHost.Visibility = output ? Visibility.Visible : Visibility.Collapsed;
        ProblemsHost.Visibility = output ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void About_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "Keyce Code v1.0\nMini-IDE Windows inspiré de Visual Studio Code.",
            "À propos", MessageBoxButton.OK, MessageBoxImage.Information);
}
