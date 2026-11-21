using Keyce.Models;

namespace Keyce.Services;

/// <summary>
/// Détecte les outils installés sur la machine (recherche dans le PATH).
/// Le résultat est affiché dans la barre d'état.
/// </summary>
public interface IToolchainDetector
{
    /// <summary>Cherche dotnet (priorité), python, node, java/javac et gcc.</summary>
    Task<IReadOnlyList<ToolInfo>> DetectAsync(CancellationToken cancellationToken = default);
}
