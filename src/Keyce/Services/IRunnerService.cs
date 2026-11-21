using Keyce.Models;

namespace Keyce.Services;

/// <summary>
/// Exécute un fichier de code et renvoie le résultat.
/// </summary>
public interface IRunnerService
{
    /// <summary>True pendant qu'une exécution est en cours.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Déclenché à chaque ligne de sortie, pour l'affichage en direct dans la console.
    /// Le booléen vaut true si la ligne vient de la sortie d'erreur.
    /// </summary>
    event EventHandler<(string Line, bool IsError)>? OutputReceived;

    /// <summary>
    /// Exécute un projet ou un fichier et attend la fin. Annulable avec le jeton.
    /// path = dossier contenant un .csproj (dotnet build puis dotnet run)
    /// ou chemin d'un fichier à lancer (.py, .js, .java, .c).
    /// </summary>
    Task<ExecutionResult> RunAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Arrête l'exécution en cours, s'il y en a une.</summary>
    void Stop();
}
