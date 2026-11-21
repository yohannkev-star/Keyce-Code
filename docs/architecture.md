# Architecture de Keyce

## Principe

Application WPF organisée en **MVVM** (Model - View - ViewModel). Les vues n'ont pas de logique,
les ViewModels pilotent l'interface, les services font le travail technique.

```
View (XAML)  <-->  ViewModel  -->  Service (interface)  -->  Système (fichiers, processus)
                       |
                     Models
```

## Structure du dépôt

| Dossier | Contenu |
|---|---|
| `src/Keyce/Views` | Fenêtres et pages XAML |
| `src/Keyce/Controls` | Contrôles réutilisables |
| `src/Keyce/ViewModels` | Logique de présentation |
| `src/Keyce/Models` | Objets de données (`ExecutionResult`, `EditorDocument`) |
| `src/Keyce/Services` | Interfaces et implémentations des services |
| `src/Keyce/Styles` | Dictionnaires de ressources (couleurs, styles) |
| `docs` | Documentation |
| `tests` | Projets de tests |

## Contrat commun (ne pas modifier sans accord du groupe)

Les trois interfaces ci-dessous sont le contrat entre les membres. Chacun développe contre
l'interface, pas contre l'implémentation du voisin.

- `IFileService` : lecture, écriture, liste, suppression de fichiers.
- `IEditorService` : documents ouverts, contenu, enregistrement.
- `IRunnerService` : exécution d'un fichier, sortie en direct, arrêt.
- `ExecutionResult` : résultat d'une exécution (code de sortie, sorties, durée, annulation).

Tout changement d'une interface passe par une Pull Request relue par toute l'équipe.

## Règles de code

- Un ViewModel ne référence jamais une View.
- Les accès disque passent par `IFileService`, jamais par `System.IO` directement.
- Les opérations longues sont `async` et annulables (`CancellationToken`).
- Une classe, un fichier. Noms en anglais, commentaires en français.

## Règles Git

- Personne ne pousse sur `main`.
- Une branche par tâche : `feature/nom-court`, `fix/nom-court`.
- Une Pull Request par branche, relue par au moins un autre membre, fusionnée par Yohann.
- Messages de commit : `feat: ...`, `fix: ...`, `docs: ...`, `chore: ...`.
- Avant de pousser : `git pull origin main` puis `dotnet build`. Pas de push si le build échoue.

## Cible technique

.NET 10 (`net10.0-windows`), SDK 10 ou plus récent requis.
