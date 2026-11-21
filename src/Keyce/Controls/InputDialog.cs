using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Keyce.Controls;

/// <summary>Petite boîte de dialogue réutilisable pour les commandes de navigation.</summary>
internal sealed class InputDialog : Window
{
    private readonly TextBox _valueBox;

    public InputDialog(string title, string prompt, string initialValue)
    {
        Title = title;
        Width = 360;
        Height = 145;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _valueBox = new TextBox { Text = initialValue, Margin = new Thickness(0, 8, 0, 12) };
        var okButton = new Button { Content = "OK", IsDefault = true, MinWidth = 72, Padding = new Thickness(10, 4, 10, 4) };
        var cancelButton = new Button { Content = "Annuler", IsCancel = true, MinWidth = 72, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
        okButton.Click += (_, _) => { DialogResult = true; Close(); };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);
        var layout = new StackPanel { Margin = new Thickness(14) };
        layout.Children.Add(new TextBlock { Text = prompt });
        layout.Children.Add(_valueBox);
        layout.Children.Add(buttons);
        Content = layout;
        Loaded += (_, _) => { _valueBox.Focus(); _valueBox.SelectAll(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };
    }

    public string Value => _valueBox.Text;
}
