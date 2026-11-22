namespace Keyce.Models;

/// <summary>Erreur ou avertissement localisé dans un fichier.</summary>
public record ParsedError(string File, int Line, int Column, string Severity, string Message);