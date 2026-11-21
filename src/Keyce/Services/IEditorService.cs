using Keyce.Models;

namespace Keyce.Services;

/// <summary>
/// Gère les documents ouverts dans l'éditeur (ouverture, modification, enregistrement).
/// Utilise IFileService pour les accès disque.
/// </summary>
public interface IEditorService
{
    /// <summary>Documents actuellement ouverts.</summary>
    IReadOnlyList<EditorDocument> OpenDocuments { get; }

    /// <summary>Document actif, ou null si aucun.</summary>
    EditorDocument? ActiveDocument { get; }

    /// <summary>Déclenché quand le document actif change.</summary>
    event EventHandler? ActiveDocumentChanged;

    /// <summary>Crée un document vide, non enregistré.</summary>
    EditorDocument NewDocument();

    /// <summary>Ouvre un fichier existant et le rend actif.</summary>
    Task<EditorDocument> OpenAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Met à jour le contenu d'un document et le marque comme modifié.</summary>
    void UpdateContent(EditorDocument document, string content);

    /// <summary>Enregistre le document. Si path est fourni, c'est un « Enregistrer sous ».</summary>
    Task SaveAsync(EditorDocument document, string? path = null, CancellationToken cancellationToken = default);

    /// <summary>Ferme un document (l'appelant gère la confirmation si IsDirty).</summary>
    void Close(EditorDocument document);
}
