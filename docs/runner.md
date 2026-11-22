# Module Exécution et console (Développeur 4)

Ce module permet de lancer un projet ou un fichier avec les outils déjà installés
sur la machine (Keyce Code n'embarque aucun compilateur) et d'afficher le résultat
en direct dans le panneau Sortie.

## Fichiers du module

| Fichier | Rôle |
|---|---|
| `Services/IToolchainDetector.cs` | Contrat de détection des outils |
| `Services/ToolchainDetector.cs` | Cherche dotnet, python, node, java, javac, gcc dans le PATH |
| `Services/IRunnerService.cs` | Contrat d'exécution (`RunAsync`, `Stop`, `OutputReceived`) |
| `Services/ProcessRunner.cs` | Implémentation : lance les processus, sortie en direct, annulation |
| `Services/ErrorParser.cs` | Reconnaît les erreurs MSBuild, gcc, javac et Python |
| `Models/ParsedError.cs` | Erreur localisée (fichier, ligne, colonne, gravité, message) |
| `Models/ExecutionResult.cs` | Résultat d'une exécution (code de sortie, sorties, durée, annulation) |
| `Views/OutputPanel.xaml(.cs)` | Panneau Sortie : couleurs, Stop, Effacer, erreurs cliquables |

## Outils détectés

Priorité au SDK .NET, puis Python, Node.js, Java (java et javac) et GCC.
La détection se fait une seule fois au démarrage, sans bloquer la fenêtre, et le
résultat est affiché dans la barre d'état. Si un outil manque, la console affiche
un message clair au lieu de planter.

## Fichiers pris en charge

| Chemin donné à `RunAsync` | Commande lancée |
|---|---|
| Dossier contenant un `.csproj` | `dotnet build` puis `dotnet run` |
| `.py` | `python fichier.py` |
| `.js` | `node fichier.js` |
| `.java` | `javac` puis `java` **(à vérifier selon l'état de ProcessRunner)** |
| `.c` | `gcc` puis exécution du `.exe` **(à vérifier selon l'état de ProcessRunner)** |

## Fonctionnement

1. L'utilisateur appuie sur F5 : tous les fichiers sont enregistrés.
2. `IRunnerService.RunAsync(chemin)` lance les étapes une par une.
3. Chaque ligne de sortie déclenche `OutputReceived` avec `(Line, IsError)`.
4. `OutputPanel` affiche la ligne : stderr et erreurs en rouge, avertissements en jaune.
5. À la fin, `RunAsync` renvoie un `ExecutionResult` (code de sortie, durée, annulation).

Le bouton Stop appelle `Stop()` : le processus et ses enfants sont arrêtés et le
résultat est marqué `WasCancelled`.

## Brancher le panneau Sortie

Une seule fois, à l'initialisation de la fenêtre :

```csharp
runner.OutputReceived += output.OnOutputReceived;
output.StopRequested  += () => runner.Stop();
output.ErrorClicked   += (file, line) => /* ouvrir le fichier à la ligne (éditeur) */;
```

À chaque F5, après « Tout enregistrer » :

```csharp
if (!runner.IsRunning)
{
    output.Clear();
    await runner.RunAsync(cheminDuProjetOuFichier);
}
```

Notes :
- `OutputReceived` est déclenché depuis un thread secondaire : `OutputPanel` passe
  lui-même par le Dispatcher, l'appelant n'a rien à faire.
- `RunAsync` lève une exception si une exécution est déjà en cours : vérifier
  `IsRunning` avant d'appeler.
- Les chemins d'erreur Python peuvent être relatifs : les combiner avec le dossier
  du projet avant d'ouvrir le fichier.

## Couleurs

Aucune couleur en dur. Le panneau utilise les clés de `Styles/Colors.xaml` :
`Panel.Background`, `Panel.TabActiveForeground`, `Text.Default`,
`Output.Info`, `Output.Error`, `Output.Warning`.

## Tester

```bash
dotnet test
```

Tests fournis : analyse des erreurs (`ErrorParserTests`), `ExecutionResult`
(`ExecutionResultTests`), détecteur et runner (`RunnerTests`).

Test manuel (hors dépôt) :

```bash
dotnet new console -o C:\temp\essai
```

Ouvrir le dossier dans Keyce Code, appuyer sur F5, vérifier la sortie. Introduire
une erreur volontaire : le message du compilateur doit s'afficher en rouge et un
clic doit ouvrir le fichier à la bonne ligne.

## Limites connues

- Pas de saisie utilisateur (`Console.ReadLine` reste bloqué : utiliser Stop).
- Pas de débogueur.
- Si un outil n'est pas dans le PATH, la console l'indique, rien n'est installé automatiquement.