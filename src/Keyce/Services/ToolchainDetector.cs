using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Keyce.Models;

namespace Keyce.Services;

/// <summary>
/// Détecte les outils installés (dotnet en priorité, puis python, node, java, javac, gcc)
/// en les cherchant dans le PATH et en lisant leur version.
/// </summary>
public sealed class ToolchainDetector : IToolchainDetector
{
    // Nom affiché, nom de l'exécutable, argument pour obtenir la version.
    private static readonly (string Name, string Command, string Args)[] Tools =
    {
        ("dotnet", "dotnet", "--version"),
        ("python", "python", "--version"),
        ("node", "node", "--version"),
        ("java", "java", "-version"),
        ("javac", "javac", "-version"),
        ("gcc", "gcc", "--version"),
    };

    private static readonly string[] Extensions = { ".exe", ".cmd", ".bat" };

    public async Task<IReadOnlyList<ToolInfo>> DetectAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ToolInfo>();

        foreach (var (name, command, args) in Tools)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = FindInPath(command);
            if (path is null)
            {
                continue;
            }

            var version = await GetVersionAsync(path, args, cancellationToken);
            if (version is null)
            {
                continue;
            }

            results.Add(new ToolInfo(name, version, path));
        }

        return results;
    }

    /// <summary>Cherche un exécutable dans les dossiers du PATH. Renvoie null s'il est absent.</summary>
    private static string? FindInPath(string command)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in Extensions)
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim().Trim('"'), command + extension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // Dossier invalide dans le PATH : on l'ignore.
                }
            }
        }

        return null;
    }

    /// <summary>Lance l'outil avec son argument de version et renvoie la première ligne, ou null si échec.</summary>
    private static async Task<string?> GetVersionAsync(string path, string args, CancellationToken cancellationToken)
    {
        Process? process = null;

        try
        {
            var startInfo = new ProcessStartInfo(path, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            // On limite l'attente à 5 secondes pour ne jamais bloquer le démarrage.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);

            if (process.ExitCode != 0)
            {
                return null;
            }

            // java et javac écrivent leur version sur la sortie d'erreur.
            var text = (await outputTask) + "\n" + (await errorTask);
            var firstLine = text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(firstLine) ? null : firstLine;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null; // délai dépassé
        }
        catch (Exception)
        {
            return null; // outil introuvable ou inutilisable
        }
        finally
        {
            if (process is { HasExited: false })
            {
                try { process.Kill(true); } catch { /* déjà terminé */ }
            }

            process?.Dispose();
        }
    }
}
