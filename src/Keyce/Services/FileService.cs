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

    public class FileService
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

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