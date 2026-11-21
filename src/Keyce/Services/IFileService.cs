namespace Keyce.Services;

/// <summary>
/// Accès au système de fichiers : lecture, écriture, création, suppression.
/// Aucune autre classe ne doit utiliser System.IO directement pour ces opérations.
/// </summary>
public interface IFileService
{
    /// <summary>Lit tout le texte d'un fichier.</summary>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Écrit le texte dans un fichier (le crée ou l'écrase).</summary>
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Indique si le fichier existe.</summary>
    bool FileExists(string path);

    /// <summary>Liste les fichiers d'un dossier (non récursif).</summary>
    IReadOnlyList<string> GetFiles(string directoryPath);

    /// <summary>Liste les sous-dossiers d'un dossier (non récursif).</summary>
    IReadOnlyList<string> GetDirectories(string directoryPath);

    /// <summary>Supprime un fichier.</summary>
    void DeleteFile(string path);

    /// <summary>Renomme ou déplace un fichier.</summary>
    void MoveFile(string oldPath, string newPath);
}
