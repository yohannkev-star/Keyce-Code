using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Keyce.Models;

namespace Keyce.Services
{
    public enum CloseDecision { Save, Discard, Cancel }

    /// <summary>
    /// Enregistrement des documents de l'éditeur : Ctrl+S, Tout enregistrer,
    /// et alerte de modifications non enregistrées.
    /// </summary>
    public class SaveService
    {
        private readonly FileService _files;

        /// <summary>Fournit la liste des documents ouverts (à brancher une fois dans MainWindow).</summary>
        public Func<IEnumerable<EditorDocument>> GetOpenDocuments { get; set; } = () => Array.Empty<EditorDocument>();

        /// <summary>Demande un chemin pour un document sans fichier (Enregistrer sous). Retourne null si annulé.</summary>
        public Func<EditorDocument, string?> AskSavePath { get; set; } = _ => null;

        public SaveService(FileService files)
        {
            _files = files;
        }

        // ---------- Ctrl+S ----------
        public FileResult Save(EditorDocument doc)
        {
            string? path = doc.FilePath ?? AskSavePath(doc);
            if (path == null)
                return new FileResult { Success = false, Error = "Enregistrement annulé." };

            var result = _files.WriteFile(path, doc.Content);
            if (result.Success)
            {
                doc.FilePath = path;
                doc.IsDirty = false;
            }
            return result;
        }

        // ---------- Tout enregistrer ----------
        /// <summary>Retourne la liste des erreurs (vide si tout s'est bien passé).</summary>
        public List<string> SaveAll()
        {
            var errors = new List<string>();
            foreach (var doc in GetOpenDocuments().Where(d => d.IsDirty).ToList())
            {
                var result = Save(doc);
                if (!result.Success) errors.Add(result.Error);
            }
            return errors;
        }

        // ---------- Alerte de modifications non enregistrées ----------
        public bool HasUnsavedChanges() => GetOpenDocuments().Any(d => d.IsDirty);

        /// <summary>
        /// À appeler avant de fermer UN onglet.
        /// Retourne true si on peut fermer, false si l'utilisateur annule.
        /// </summary>
        public bool ConfirmCloseDocument(EditorDocument doc)
        {
            if (!doc.IsDirty) return true;

            switch (Ask("Enregistrer les modifications de « " + doc.DisplayName + " » avant de fermer ?"))
            {
                case CloseDecision.Save: return Save(doc).Success;
                case CloseDecision.Discard: return true;
                default: return false;
            }
        }

        /// <summary>
        /// À appeler quand on ferme la fenêtre principale.
        /// Retourne true si l'application peut se fermer.
        /// </summary>
        public bool ConfirmCloseApplication()
        {
            if (!HasUnsavedChanges()) return true;

            switch (Ask("Des fichiers ont des modifications non enregistrées. Tout enregistrer avant de quitter ?"))
            {
                case CloseDecision.Save: return SaveAll().Count == 0;
                case CloseDecision.Discard: return true;
                default: return false;
            }
        }

        private static CloseDecision Ask(string message)
        {
            var answer = MessageBox.Show(message, "Keyce Code",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

            return answer switch
            {
                MessageBoxResult.Yes => CloseDecision.Save,
                MessageBoxResult.No => CloseDecision.Discard,
                _ => CloseDecision.Cancel
            };
        }
    }
}