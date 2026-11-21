using System.Windows.Input;

namespace Keyce;

/// <summary>
/// Commandes de l'application et raccourcis clavier (style VS Code).
/// Ctrl+N / Ctrl+O / Ctrl+S / Ctrl+F utilisent les ApplicationCommands standard.
/// </summary>
public static class AppCommands
{
    private static RoutedUICommand Make(string text, string name, Key key, ModifierKeys mods = ModifierKeys.None)
        => new(text, name, typeof(AppCommands), new InputGestureCollection { new KeyGesture(key, mods) });

    public static readonly RoutedUICommand OpenFolder    = Make("Ouvrir un dossier", nameof(OpenFolder), Key.O, ModifierKeys.Control | ModifierKeys.Shift);
    public static readonly RoutedUICommand NewProject    = Make("Nouveau projet Console C#", nameof(NewProject), Key.N, ModifierKeys.Control | ModifierKeys.Shift);
    public static readonly RoutedUICommand SaveAll       = Make("Tout enregistrer", nameof(SaveAll), Key.S, ModifierKeys.Control | ModifierKeys.Shift);
    public static readonly RoutedUICommand CloseTab      = Make("Fermer l'onglet", nameof(CloseTab), Key.W, ModifierKeys.Control);
    public static readonly RoutedUICommand GoToLine      = Make("Aller à la ligne", nameof(GoToLine), Key.G, ModifierKeys.Control);
    public static readonly RoutedUICommand ToggleExplorer = Make("Explorateur", nameof(ToggleExplorer), Key.B, ModifierKeys.Control);
    public static readonly RoutedUICommand TogglePanel   = Make("Panneau", nameof(TogglePanel), Key.OemTilde, ModifierKeys.Control);
    public static readonly RoutedUICommand Run           = Make("Exécuter", nameof(Run), Key.F5);
    public static readonly RoutedUICommand Stop          = Make("Arrêter", nameof(Stop), Key.F5, ModifierKeys.Shift);
    public static readonly RoutedUICommand ClearOutput   = new("Effacer la sortie", nameof(ClearOutput), typeof(AppCommands));
}
