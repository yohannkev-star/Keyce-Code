using System.Windows;
using System.Windows.Controls;

namespace Keyce.Views;

public partial class OutputPanel : UserControl
{
    /// <summary>Bouton Stop cliqué : l'intégrateur appelle runner.Stop().</summary>
    public event Action? StopRequested;

    public OutputPanel() => InitializeComponent();

    public void Clear()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(Clear); return; }
        Para.Inlines.Clear();
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke();
    private void Clear_Click(object sender, RoutedEventArgs e) => Clear();
}