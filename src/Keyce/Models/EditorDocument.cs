using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;

namespace Keyce.Models;

/// <summary>Un document ouvert dans l'éditeur.</summary>
public sealed class EditorDocument : INotifyPropertyChanged
{
    private string? _filePath;
    private string _content = string.Empty;
    private bool _isDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Chemin complet du fichier, ou null si le document n'a pas encore été enregistré.</summary>
    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value) return;
            _filePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>Contenu actuel du texte dans l'éditeur.</summary>
    public string Content
    {
        get => _content;
        set
        {
            if (_content == value) return;
            _content = value;
            OnPropertyChanged();
        }
    }

    /// <summary>True si le contenu a changé depuis le dernier enregistrement.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Nom affiché dans l'onglet.</summary>
    public string DisplayName =>
        string.IsNullOrEmpty(FilePath) ? "Sans titre" : Path.GetFileName(FilePath);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
