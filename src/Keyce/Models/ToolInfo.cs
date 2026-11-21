namespace Keyce.Models;

/// <summary>Un outil de développement détecté sur la machine (dotnet, python, node, java, gcc).</summary>
public sealed record ToolInfo(string Name, string Version, string Path);
