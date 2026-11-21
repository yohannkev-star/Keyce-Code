namespace Keyce.Models;

/// <summary>
/// Résultat d'une exécution de code, renvoyé par IRunnerService.
/// Objet immuable : on ne le modifie pas après création.
/// </summary>
public sealed record ExecutionResult
{
    /// <summary>True si le processus s'est terminé avec le code 0 et sans annulation.</summary>
    public bool Success => ExitCode == 0 && !WasCancelled;

    /// <summary>Code de sortie du processus (0 = succès).</summary>
    public int ExitCode { get; init; }

    /// <summary>Texte écrit sur la sortie standard.</summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>Texte écrit sur la sortie d'erreur.</summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>Durée totale de l'exécution.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>True si l'utilisateur a arrêté l'exécution avant la fin.</summary>
    public bool WasCancelled { get; init; }

    public static ExecutionResult Cancelled(TimeSpan duration) =>
        new() { ExitCode = -1, WasCancelled = true, Duration = duration };
}
