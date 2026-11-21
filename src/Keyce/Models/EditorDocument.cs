namespace Keyce.Models;

/// <summary>Un document ouvert dans l'éditeur.</summary>
public sealed class EditorDocument
{
    /// <summary>Chemin complet du fichier, ou null si le document n'a pas encore été enregistré.</summary>
    public string? FilePath { get; set; }

    /// <summary>Contenu actuel du texte dans l'éditeur.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>True si le contenu a changé depuis le dernier enregistrement.</summary>
    public bool IsDirty { get; set; }

    /// <summary>Nom affiché dans l'onglet.</summary>
    public string DisplayName =>
        string.IsNullOrEmpty(FilePath) ? "Sans titre" : System.IO.Path.GetFileName(FilePath);
}

