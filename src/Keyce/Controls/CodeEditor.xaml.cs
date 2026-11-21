using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using Keyce.Models;

namespace Keyce.Controls;

/// <summary>Éditeur AvalonEdit réutilisable avec navigation, recherche et coloration par extension.</summary>
public partial class CodeEditor : UserControl
{
    private EditorDocument? _document;
    private bool _loadingDocument;

    public CodeEditor()
    {
        InitializeComponent();
        TextEditorControl.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        TextEditorControl.TextArea.PreviewKeyDown += TextArea_PreviewKeyDown;
        TextEditorControl.PreviewMouseWheel += TextEditor_PreviewMouseWheel;
        TextEditorControl.TextChanged += TextEditorControl_TextChanged;
    }

    public event EventHandler? TextChanged;
    public event EventHandler? CaretPositionChanged;

    public TextEditor Editor => TextEditorControl;
    public string Text => TextEditorControl.Text;
    public int CaretLine => TextEditorControl.TextArea.Caret.Line;
    public int CaretColumn => TextEditorControl.TextArea.Caret.Column;

    /// <summary>Charge un document sans le marquer comme modifié.</summary>
    public void LoadDocument(EditorDocument? document)
    {
        _document = document;
        _loadingDocument = true;
        try
        {
            TextEditorControl.Text = document?.Content ?? string.Empty;
            TextEditorControl.TextArea.ClearSelection();
            TextEditorControl.CaretOffset = 0;
            ApplyHighlighting(document?.FilePath);
        }
        finally
        {
            _loadingDocument = false;
        }
    }

    public string GetText() => TextEditorControl.Text;

    /// <summary>Place le curseur à la ligne demandée et rend cette ligne visible.</summary>
    public void GoToLine(int lineNumber)
    {
        var document = TextEditorControl.Document;
        if (document.LineCount == 0)
            return;

        var line = document.GetLineByNumber(Math.Clamp(lineNumber, 1, document.LineCount));
        TextEditorControl.CaretOffset = line.Offset;
        TextEditorControl.ScrollTo(line.LineNumber, 1);
        TextEditorControl.Focus();
    }

    public void FocusEditor() => TextEditorControl.Focus();

    public void ShowFind()
    {
        FindBar.Visibility = Visibility.Visible;
        FindTextBox.Focus();
        FindTextBox.SelectAll();
    }

    private void TextEditorControl_TextChanged(object? sender, EventArgs e)
    {
        if (_loadingDocument)
            return;

        if (_document is not null)
            _document.Content = TextEditorControl.Text;
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Caret_PositionChanged(object? sender, EventArgs e) =>
        CaretPositionChanged?.Invoke(this, EventArgs.Empty);

    private void TextArea_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowFind();
            e.Handled = true;
        }
        else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
        {
            PromptForLine();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && FindBar.Visibility == Visibility.Visible)
        {
            CloseFind();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            InsertIndentedNewLine();
            e.Handled = true;
        }
    }

    private void TextEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        TextEditorControl.FontSize = Math.Clamp(TextEditorControl.FontSize + (e.Delta > 0 ? 1 : -1), 8, 40);
        e.Handled = true;
    }

    private void InsertIndentedNewLine()
    {
        var document = TextEditorControl.Document;
        var selectionStart = TextEditorControl.SelectionStart;
        var line = document.GetLineByOffset(selectionStart);
        var prefix = document.GetText(line.Offset, Math.Max(0, selectionStart - line.Offset));
        var indent = new string(prefix.TakeWhile(character => character is ' ' or '\t').ToArray());
        if (prefix.TrimEnd().EndsWith('{'))
            indent += "    ";

        TextEditorControl.SelectedText = Environment.NewLine + indent;
        TextEditorControl.CaretOffset = selectionStart + Environment.NewLine.Length + indent.Length;
    }

    private void ApplyHighlighting(string? path)
    {
        var extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
        var definitionName = extension switch
        {
            ".cs" => "C#",
            ".js" or ".jsx" or ".ts" or ".tsx" => "JavaScript",
            ".java" => "Java",
            ".cpp" or ".cc" or ".cxx" or ".h" or ".hpp" => "C++",
            ".c" => "C",
            ".py" => "Python",
            _ => null
        };

        TextEditorControl.SyntaxHighlighting = definitionName is null
            ? null
            : HighlightingManager.Instance.GetDefinition(definitionName);
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => Find(1);
    private void FindPrevious_Click(object sender, RoutedEventArgs e) => Find(-1);
    private void CloseFind_Click(object sender, RoutedEventArgs e) => CloseFind();

    private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Find(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseFind();
            e.Handled = true;
        }
    }

    private void Find(int direction)
    {
        var query = FindTextBox.Text;
        var text = TextEditorControl.Text;
        if (string.IsNullOrEmpty(query) || text.Length == 0)
            return;

        var selectionEnd = TextEditorControl.SelectionStart + TextEditorControl.SelectionLength;
        var index = direction > 0
            ? text.IndexOf(query, Math.Clamp(selectionEnd, 0, text.Length), StringComparison.OrdinalIgnoreCase)
            : text.LastIndexOf(query, Math.Clamp(TextEditorControl.SelectionStart - 1, 0, text.Length - 1), StringComparison.OrdinalIgnoreCase);

        if (index < 0)
            index = direction > 0
                ? text.IndexOf(query, StringComparison.OrdinalIgnoreCase)
                : text.LastIndexOf(query, text.Length - 1, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return;

        TextEditorControl.Select(index, query.Length);
        TextEditorControl.ScrollToLine(TextEditorControl.TextArea.Caret.Line);
        TextEditorControl.Focus();
    }

    private void CloseFind()
    {
        FindBar.Visibility = Visibility.Collapsed;
        FocusEditor();
    }

    public void PromptForLine()
    {
        var dialog = new InputDialog("Aller à la ligne", "Numéro de ligne :", CaretLine.ToString())
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true && int.TryParse(dialog.Value, out var line))
            GoToLine(line);
    }
}
