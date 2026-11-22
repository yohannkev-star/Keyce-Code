using System;
using System.IO;
using System.Text;

namespace Keyce.Services
{
    public class FileResult
    {
        public bool Success { get; set; }
        public string Content { get; set; } = "";
        public string Error { get; set; } = "";
    }

    public class FileService : IFileService
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);

        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) =>
            File.WriteAllTextAsync(path, content, Utf8, cancellationToken);

        public bool FileExists(string path) => File.Exists(path);

        public IReadOnlyList<string> GetFiles(string directoryPath) => Directory.GetFiles(directoryPath);

        public IReadOnlyList<string> GetDirectories(string directoryPath) => Directory.GetDirectories(directoryPath);

        public void DeleteFile(string path) => File.Delete(path);

        public void MoveFile(string oldPath, string newPath) => File.Move(oldPath, newPath);

        public FileResult ReadFile(string path)
        {
            try
            {
                return new FileResult { Success = true, Content = File.ReadAllText(path, Encoding.UTF8) };
            }
            catch (FileNotFoundException)
            {
                return Fail($"Fichier introuvable : {path}");
            }
            catch (DirectoryNotFoundException)
            {
                return Fail($"Dossier introuvable : {path}");
            }
            catch (UnauthorizedAccessException)
            {
                return Fail($"Accès refusé : {path}");
            }
            catch (IOException ex)
            {
                return Fail($"Erreur de lecture : {ex.Message}");
            }
        }

        public FileResult WriteFile(string path, string content)
        {
            try
            {
                File.WriteAllText(path, content, Utf8);
                return new FileResult { Success = true };
            }
            catch (UnauthorizedAccessException)
            {
                return Fail($"Accès refusé : {path}");
            }
            catch (DirectoryNotFoundException)
            {
                return Fail($"Dossier introuvable : {path}");
            }
            catch (IOException ex)
            {
                return Fail($"Erreur d'écriture : {ex.Message}");
            }
        }

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public FileResult CreateFile(string path)
        {
            if (File.Exists(path)) return Fail("Ce fichier existe déjà.");
            return WriteFile(path, "");
        }

        public FileResult CreateFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return new FileResult { Success = true };
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        private static FileResult Fail(string message) =>
            new FileResult { Success = false, Error = message };
    }
}
