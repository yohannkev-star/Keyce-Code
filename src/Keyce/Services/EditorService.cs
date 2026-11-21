using Keyce.Models;
using System.IO;

namespace Keyce.Services;

/// <summary>Implémentation des opérations sur les documents ouverts.</summary>
public sealed class EditorService : IEditorService
{
    private readonly IFileService _fileService;
    private readonly List<EditorDocument> _documents = [];

    public EditorService(IFileService fileService) => _fileService = fileService;

    public IReadOnlyList<EditorDocument> OpenDocuments => _documents;
    public EditorDocument? ActiveDocument { get; private set; }
    public event EventHandler? ActiveDocumentChanged;

    public EditorDocument NewDocument()
    {
        var document = new EditorDocument();
        _documents.Add(document);
        Activate(document);
        return document;
    }

    public async Task<EditorDocument> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var existing = _documents.FirstOrDefault(document =>
            string.Equals(document.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            Activate(existing);
            return existing;
        }

        var document = new EditorDocument
        {
            FilePath = fullPath,
            Content = await _fileService.ReadAllTextAsync(fullPath, cancellationToken)
        };
        _documents.Add(document);
        Activate(document);
        return document;
    }

    public void UpdateContent(EditorDocument document, string content)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_documents.Contains(document))
            throw new InvalidOperationException("Le document n'est pas ouvert dans l'éditeur.");
        if (document.Content == content && document.IsDirty)
            return;

        document.Content = content;
        document.IsDirty = true;
        ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveAsync(EditorDocument document, string? path = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var targetPath = path ?? document.FilePath;
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("Un chemin doit être fourni pour enregistrer ce document.", nameof(path));

        var fullPath = Path.GetFullPath(targetPath);
        var content = document.Content;
        await _fileService.WriteAllTextAsync(fullPath, content, cancellationToken);
        document.FilePath = fullPath;
        document.IsDirty = document.Content != content;
        ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Close(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_documents.Remove(document))
            return;
        if (ReferenceEquals(ActiveDocument, document))
            ActiveDocument = _documents.LastOrDefault();
        ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Activate(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_documents.Contains(document))
            throw new InvalidOperationException("Le document n'est pas ouvert dans l'éditeur.");
        if (ReferenceEquals(ActiveDocument, document))
            return;
        ActiveDocument = document;
        ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
    }
}
