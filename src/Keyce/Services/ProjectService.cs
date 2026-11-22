using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Keyce.Services
{
    // Réutilise FileResult défini dans FileService.cs
    public class ProjectService
    {
        private const int MaxRecent = 10;

        private static readonly string RecentFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Keyce", "recent.txt");

        // ---------- Projet Console C# ----------

        /// <summary>Exécute "dotnet new console" dans parentFolder\name.</summary>
        public async Task<FileResult> CreateConsoleProjectAsync(string parentFolder, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return Fail("Nom de projet invalide.");

            string target = Path.Combine(parentFolder, name);
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
                return Fail("Le dossier existe déjà et n'est pas vide.");

            try
            {
                var psi = new ProcessStartInfo("dotnet", $"new console -n \"{name}\" -o \"{target}\"")
                {
                    WorkingDirectory = parentFolder,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return Fail("Impossible de lancer dotnet.");

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                    return Fail(string.IsNullOrWhiteSpace(error) ? output : error);

                return new FileResult { Success = true, Content = target };
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return Fail("SDK .NET introuvable. Installe-le puis relance Keyce Code.");
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        /// <summary>Retourne le premier .csproj du dossier, ou null.</summary>
        public string? FindProjectFile(string folder)
        {
            try
            {
                return Directory.GetFiles(folder, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Ouvrir un .csproj = ouvrir son dossier.</summary>
        public string? GetFolderFromProjectFile(string csprojPath) =>
            File.Exists(csprojPath) ? Path.GetDirectoryName(csprojPath) : null;

        // ---------- Nouveau fichier / dossier ----------

        public FileResult CreateFile(string folder, string fileName)
        {
            if (!IsValidName(fileName)) return Fail("Nom de fichier invalide.");
            string path = Path.Combine(folder, fileName);
            if (File.Exists(path)) return Fail("Ce fichier existe déjà.");
            try
            {
                File.WriteAllText(path, "");
                return new FileResult { Success = true, Content = path };
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        public FileResult CreateFolder(string parent, string folderName)
        {
            if (!IsValidName(folderName)) return Fail("Nom de dossier invalide.");
            string path = Path.Combine(parent, folderName);
            if (Directory.Exists(path)) return Fail("Ce dossier existe déjà.");
            try
            {
                Directory.CreateDirectory(path);
                return new FileResult { Success = true, Content = path };
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        // ---------- Bonus : renommer / supprimer ----------

        public FileResult Rename(string path, string newName)
        {
            if (!IsValidName(newName)) return Fail("Nom invalide.");
            string? parent = Path.GetDirectoryName(path);
            if (parent == null) return Fail("Chemin invalide.");
            string newPath = Path.Combine(parent, newName);

            try
            {
                if (Directory.Exists(path)) Directory.Move(path, newPath);
                else if (File.Exists(path)) File.Move(path, newPath);
                else return Fail("Élément introuvable.");
                return new FileResult { Success = true, Content = newPath };
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        public FileResult Delete(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
                else return Fail("Élément introuvable.");
                return new FileResult { Success = true };
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        // ---------- Bonus : fichiers récents ----------

        public List<string> GetRecent()
        {
            try
            {
                if (!File.Exists(RecentFile)) return new List<string>();
                return File.ReadAllLines(RecentFile)
                    .Where(p => !string.IsNullOrWhiteSpace(p) && (File.Exists(p) || Directory.Exists(p)))
                    .ToList();
            }
            catch (Exception) { return new List<string>(); }
        }

        public void AddRecent(string path)
        {
            try
            {
                var list = GetRecent();
                list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
                list.Insert(0, path);
                Directory.CreateDirectory(Path.GetDirectoryName(RecentFile)!);
                File.WriteAllLines(RecentFile, list.Take(MaxRecent));
            }
            catch (Exception) { /* les récents ne doivent jamais faire planter l'app */ }
        }

        // ---------- Utilitaires ----------

        private static bool IsValidName(string name) =>
            !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        private static FileResult Fail(string message) =>
            new FileResult { Success = false, Error = message };
    }
}