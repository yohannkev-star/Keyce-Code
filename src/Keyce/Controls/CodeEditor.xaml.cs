using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Xml;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Keyce.Models;

namespace Keyce.Controls;

/// <summary>Éditeur AvalonEdit réutilisable avec navigation, recherche et coloration par extension.</summary>
public partial class CodeEditor : UserControl
{
    private static readonly Lazy<IHighlightingDefinition> CSharpDefinition =
        new(CreateCSharpDefinition);

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

        TextEditorControl.SyntaxHighlighting = extension == ".cs"
            ? CSharpDefinition.Value
            : definitionName is null
                ? null
                : HighlightingManager.Instance.GetDefinition(definitionName);
    }

    private static IHighlightingDefinition CreateCSharpDefinition()
    {
        var xshd = """
            <SyntaxDefinition name="C#" extensions=".cs" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="Keyword" foreground="%KEYWORD%" />
              <Color name="Control" foreground="%CONTROL%" />
              <Color name="Type" foreground="%TYPE%" />
              <Color name="Method" foreground="%METHOD%" />
              <Color name="String" foreground="%STRING%" />
              <Color name="Comment" foreground="%COMMENT%" />
              <Color name="Number" foreground="%NUMBER%" />
              <RuleSet>
                <Span color="Comment" begin="/\*" end="\*/" />
                <Span color="String" begin="&quot;" end="&quot;" escapeCharacter="\" />
                <Span color="String" begin="'" end="'" escapeCharacter="\" />
                <Rule color="Comment">//.*$</Rule>
                <Keywords color="Control">
                  <Word>if</Word><Word>else</Word><Word>switch</Word><Word>case</Word>
                  <Word>default</Word><Word>for</Word><Word>foreach</Word><Word>while</Word>
                  <Word>do</Word><Word>break</Word><Word>continue</Word><Word>return</Word>
                  <Word>throw</Word><Word>try</Word><Word>catch</Word><Word>finally</Word>
                  <Word>yield</Word>
                </Keywords>
                <Keywords color="Keyword">
                  <Word>abstract</Word><Word>as</Word><Word>async</Word><Word>await</Word>
                  <Word>base</Word><Word>bool</Word><Word>byte</Word><Word>char</Word>
                  <Word>checked</Word><Word>class</Word><Word>const</Word><Word>decimal</Word>
                  <Word>delegate</Word><Word>double</Word><Word>enum</Word><Word>event</Word>
                  <Word>extern</Word><Word>false</Word><Word>file</Word><Word>fixed</Word>
                  <Word>float</Word><Word>global</Word><Word>goto</Word><Word>implicit</Word>
                  <Word>in</Word><Word>int</Word><Word>interface</Word><Word>internal</Word>
                  <Word>is</Word><Word>lock</Word><Word>long</Word><Word>namespace</Word>
                  <Word>new</Word><Word>null</Word><Word>object</Word><Word>operator</Word>
                  <Word>out</Word><Word>override</Word><Word>params</Word><Word>private</Word>
                  <Word>protected</Word><Word>public</Word><Word>readonly</Word><Word>ref</Word>
                  <Word>sbyte</Word><Word>sealed</Word><Word>short</Word><Word>sizeof</Word>
                  <Word>stackalloc</Word><Word>static</Word><Word>string</Word><Word>struct</Word>
                  <Word>this</Word><Word>true</Word><Word>typeof</Word><Word>uint</Word>
                  <Word>ulong</Word><Word>unchecked</Word><Word>unsafe</Word><Word>ushort</Word>
                  <Word>using</Word><Word>virtual</Word><Word>void</Word><Word>volatile</Word>
                  <Word>var</Word><Word>record</Word><Word>required</Word><Word>init</Word>
                  <Word>with</Word><Word>when</Word><Word>where</Word><Word>get</Word><Word>set</Word>
                  <Word>add</Word><Word>remove</Word><Word>value</Word><Word>partial</Word>
                </Keywords>
                <Rule color="Number">\b(0[xX][0-9A-Fa-f]+|\d+(\.\d+)?([eE][+-]?\d+)?)[fFdDmMuUlL]*\b</Rule>
                <Rule color="Type">\b[A-Z][A-Za-z0-9_]*\b</Rule>
                <Rule color="Method">\b[A-Za-z_][A-Za-z0-9_]*(?=\s*\()</Rule>
              </RuleSet>
            </SyntaxDefinition>
            """;

        xshd = xshd
            .Replace("%KEYWORD%", GetPaletteColor("Syntax.Keyword"), StringComparison.Ordinal)
            .Replace("%CONTROL%", GetPaletteColor("Syntax.Control"), StringComparison.Ordinal)
            .Replace("%TYPE%", GetPaletteColor("Syntax.Type"), StringComparison.Ordinal)
            .Replace("%METHOD%", GetPaletteColor("Syntax.Method"), StringComparison.Ordinal)
            .Replace("%STRING%", GetPaletteColor("Syntax.String"), StringComparison.Ordinal)
            .Replace("%COMMENT%", GetPaletteColor("Syntax.Comment"), StringComparison.Ordinal)
            .Replace("%NUMBER%", GetPaletteColor("Syntax.Number"), StringComparison.Ordinal);

        using var reader = XmlReader.Create(new StringReader(xshd));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static string GetPaletteColor(string resourceKey)
    {
        var color = Application.Current?.TryFindResource(resourceKey);
        return color is Color value
            ? value.ToString()
            : throw new InvalidOperationException($"Ressource de couleur manquante : {resourceKey}");
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
