using System.Windows;
using System.Windows.Input;
using Keyce.Models;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Keyce.Services;

namespace Keyce.Views;

public enum OutputKind { Info, Error, Warning }

public partial class OutputPanel : UserControl
{
    /// <summary>Bouton Stop cliqué : l'intégrateur appelle runner.Stop().</summary>
    public event Action? StopRequested;
        /// <summary>Clic sur une erreur : (fichier, ligne). Relié à l'éditeur (D2).</summary>
    public event Action<string, int>? ErrorClicked;

    public OutputPanel() => InitializeComponent();

    /// <summary>Branchement direct : runner.OutputReceived += output.OnOutputReceived;</summary>
    public void OnOutputReceived(object? sender, (string Line, bool IsError) e) =>
        AppendLine(e.Line, e.IsError);

    /// <summary>Choisit la couleur selon le contenu de la ligne.</summary>
    public void AppendLine(string line, bool isError)
    {
        var kind = isError ? OutputKind.Error : OutputKind.Info;
        var parsed = ErrorParser.Parse(line);
        if (parsed != null)
            kind = parsed.Severity == "warning" ? OutputKind.Warning : OutputKind.Error;
        else if (line.Contains("warning", StringComparison.OrdinalIgnoreCase))
            kind = OutputKind.Warning;
        Append(line, kind, parsed);
    }

        public void Append(string text, OutputKind kind = OutputKind.Info, ParsedError? parsed = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => Append(text, kind, parsed));
            return;
        }

        var run = new Run(text + "\n");
        if (BrushFor(kind) is Brush brush) run.Foreground = brush;

        if (parsed != null)
        {
            run.Cursor = Cursors.Hand;
            run.TextDecorations = TextDecorations.Underline;
            run.MouseLeftButtonUp += (_, _) => ErrorClicked?.Invoke(parsed.File, parsed.Line);
        }

        Para.Inlines.Add(run);
        if (Para.Inlines.Count > 5000) Para.Inlines.Remove(Para.Inlines.FirstInline!);
        Output.ScrollToEnd();
    }

    public void Clear()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(Clear); return; }
        Para.Inlines.Clear();
    }

    // Couleurs issues de Colors.xaml (jamais en dur)
    private Brush? BrushFor(OutputKind kind) => kind switch
    {
        OutputKind.Error   => TryFindResource("Output.Error") as Brush,
        OutputKind.Warning => TryFindResource("Output.Warning") as Brush,
        _                  => TryFindResource("Output.Info") as Brush
    };

    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke();
    private void Clear_Click(object sender, RoutedEventArgs e) => Clear();
}