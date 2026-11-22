using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Keyce.ViewModels
{
    public class ExplorerNode : INotifyPropertyChanged
    {
        private static readonly HashSet<string> Ignored =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".git", ".vs" };

        private bool _isExpanded;
        private bool _loaded;

        public string Name { get; }
        public string FullPath { get; }
        public bool IsDirectory { get; }
        public ObservableCollection<ExplorerNode> Children { get; } = new ObservableCollection<ExplorerNode>();

        // Icônes Segoe MDL2 Assets : dossier / fichier
        public string Icon => IsDirectory ? "\uE8B7" : "\uE7C3";

        public ExplorerNode(string path, bool isDirectory)
        {
            FullPath = path;
            IsDirectory = isDirectory;
            Name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(Name)) Name = path; // racine de lecteur

            // Noeud factice : affiche la flèche d'expansion sans charger le contenu
            if (isDirectory) Children.Add(new ExplorerNode());
        }

        // Constructeur du noeud factice
        private ExplorerNode()
        {
            Name = "";
            FullPath = "";
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                if (value && IsDirectory && !_loaded) LoadChildren();
                OnPropertyChanged();
            }
        }

        public void LoadChildren()
        {
            _loaded = true;
            Children.Clear();
            try
            {
                var dirs = Directory.GetDirectories(FullPath)
                    .Where(d => !Ignored.Contains(Path.GetFileName(d)))
                    .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);

                var files = Directory.GetFiles(FullPath)
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

                foreach (var d in dirs) Children.Add(new ExplorerNode(d, true));
                foreach (var f in files) Children.Add(new ExplorerNode(f, false));
            }
            catch (UnauthorizedAccessException) { /* dossier protégé : on l'affiche vide */ }
            catch (IOException) { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ExplorerViewModel : INotifyPropertyChanged
    {
        private string _rootPath = "";

        public ObservableCollection<ExplorerNode> Nodes { get; } = new ObservableCollection<ExplorerNode>();

        public string RootPath
        {
            get => _rootPath;
            private set { _rootPath = value; OnPropertyChanged(); }
        }

        // Déclenché quand on double-clique sur un fichier : l'éditeur (Dév. 2) s'y abonne
        public event Action<string>? FileOpenRequested;

        public void OpenFolder(string path)
        {
            if (!Directory.Exists(path)) return;

            RootPath = path;
            Nodes.Clear();

            var root = new ExplorerNode(path, true);
            Nodes.Add(root);
            root.IsExpanded = true;
        }

        public void Refresh()
        {
            if (!string.IsNullOrEmpty(RootPath)) OpenFolder(RootPath);
        }

        // À appeler depuis le double-clic de la vue
        public void ActivateNode(ExplorerNode? node)
        {
            if (node == null || node.IsDirectory) return;
            FileOpenRequested?.Invoke(node.FullPath);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}