using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Keyce.Models;

namespace Keyce.Services;

/// <summary>
/// Lance un projet ou un fichier avec les outils installés sur la machine
/// et renvoie le résultat. Ne bloque jamais la fenêtre : tout est asynchrone.
/// Attention : OutputReceived est déclenché depuis un thread secondaire,
/// l'interface doit passer par le Dispatcher pour afficher les lignes.
/// </summary>
public sealed class ProcessRunner : IRunnerService
{
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;

    public bool IsRunning { get; private set; }

    public event EventHandler<(string Line, bool IsError)>? OutputReceived;

    public async Task<ExecutionResult> RunAsync(string path, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource cts;

        lock (_lock)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Une exécution est déjà en cours.");
            }

            IsRunning = true;
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cts = cts;
        }

        var stopwatch = Stopwatch.StartNew();
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            var steps = BuildSteps(path, out var errorMessage);
            if (steps is null)
            {
                Emit(errorMessage!, true, stderr);
                return new ExecutionResult
                {
                    ExitCode = -1,
                    StandardError = stderr.ToString(),
                    Duration = stopwatch.Elapsed,
                };
            }

            var exitCode = 0;
            foreach (var step in steps)
            {
                // Rappel de la commande lancée, affiché dans la console uniquement.
                Emit($"> {step.FileName} {string.Join(' ', step.Arguments)}", false, null);

                exitCode = await RunStepAsync(step, stdout, stderr, cts.Token);
                if (exitCode != 0)
                {
                    break;
                }
            }

            return new ExecutionResult
            {
                ExitCode = exitCode,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString(),
                Duration = stopwatch.Elapsed,
            };
        }
        catch (OperationCanceledException)
        {
            return ExecutionResult.Cancelled(stopwatch.Elapsed) with
            {
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString(),
            };
        }
        finally
        {
            lock (_lock)
            {
                IsRunning = false;
                _cts = null;
            }

            cts.Dispose();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
        }
    }

    // ---------- Préparation des commandes ----------

    private sealed record Step(string FileName, string[] Arguments, string WorkingDirectory);

    /// <summary>
    /// Dossier avec un .csproj, ou fichier .csproj : dotnet build puis dotnet run.
    /// Fichier .py ou .js : lancé directement avec python ou node.
    /// </summary>
    private static IReadOnlyList<Step>? BuildSteps(string path, out string? errorMessage)
    {
        errorMessage = null;

        if (Directory.Exists(path))
        {
            var projects = Directory.GetFiles(path, "*.csproj");
            if (projects.Length == 0)
            {
                errorMessage = $"Aucun fichier .csproj dans le dossier : {path}";
                return null;
            }

            return DotnetSteps(path, projects[0]);
        }

        if (File.Exists(path))
        {
            var directory = Path.GetDirectoryName(path) ?? ".";

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".csproj":
                    return DotnetSteps(directory, path);
                case ".py":
                    return new[] { new Step("python", new[] { path }, directory) };
                case ".c":
{
    var exe = Path.Combine(directory, Path.GetFileNameWithoutExtension(path) + ".exe");
    return new[]
    {
        new Step("gcc", new[] { path, "-o", exe }, directory),
        new Step(exe, Array.Empty<string>(), directory)
    };
}
                case ".js":
                    return new[] { new Step("node", new[] { path }, directory) };
                default:
                    errorMessage = $"Extension non prise en charge : {Path.GetExtension(path)} (pour l'instant : .csproj, .py, .js)";
                    return null;
            }
        }

        errorMessage = $"Chemin introuvable : {path}";
        return null;
    }

    private static IReadOnlyList<Step> DotnetSteps(string directory, string project) => new[]
    {
        new Step("dotnet", new[] { "build", project, "--nologo" }, directory),
        new Step("dotnet", new[] { "run", "--project", project, "--no-build" }, directory),
    };

    // ---------- Exécution d'une commande ----------

    private async Task<int> RunStepAsync(Step step, StringBuilder stdout, StringBuilder stderr, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(step.FileName)
        {
            WorkingDirectory = step.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in step.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Emit(e.Data, false, stdout);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Emit(e.Data, true, stderr);
            }
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            Emit($"Outil introuvable : {step.FileName}. Installez-le ou ajoutez-le au PATH.", true, stderr);
            return -1;
        }

        // Le programme ne peut pas lire le clavier : on ferme l'entrée pour qu'il ne reste pas bloqué.
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
                // Le processus s'est déjà arrêté.
            }

            throw;
        }

        return process.ExitCode;
    }

    /// <summary>Envoie une ligne à la console et la garde dans le texte final.</summary>
    private void Emit(string line, bool isError, StringBuilder? sink)
    {
        if (sink is not null)
        {
            lock (sink)
            {
                sink.AppendLine(line);
            }
        }

        OutputReceived?.Invoke(this, (line, isError));
    }
}
