using System.Text.RegularExpressions;
using Keyce.Models;

namespace Keyce.Services;

/// <summary>Reconnaît les messages d'erreur MSBuild, gcc, javac et Python.</summary>
public static class ErrorParser
{
    // Program.cs(12,5): error CS1002: ; expected
    private static readonly Regex MsBuild = new(
        @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s*(?<sev>error|warning)\s+(?<code>\w+):\s*(?<msg>.*)$",
        RegexOptions.Compiled);

    // f.c:5:3: error: ...
    private static readonly Regex Gcc = new(
        @"^(?<file>.+?):(?<line>\d+):(?<col>\d+):\s*(?<sev>error|warning):\s*(?<msg>.*)$",
        RegexOptions.Compiled);

    // Main.java:5: error: ...
    private static readonly Regex Javac = new(
        @"^(?<file>.+?\.java):(?<line>\d+):\s*(?<sev>error|warning):\s*(?<msg>.*)$",
        RegexOptions.Compiled);

    // File "x.py", line 3
    private static readonly Regex Python = new(
        @"File ""(?<file>.+?)"", line (?<line>\d+)",
        RegexOptions.Compiled);

    public static ParsedError? Parse(string line)
    {
        var m = MsBuild.Match(line);
        if (m.Success) return Build(m, true);

        m = Gcc.Match(line);
        if (m.Success) return Build(m, true);

        m = Javac.Match(line);
        if (m.Success) return Build(m, false);

        m = Python.Match(line);
        if (m.Success)
            return new ParsedError(m.Groups["file"].Value, int.Parse(m.Groups["line"].Value),
                1, "error", line.Trim());

        return null;
    }

    private static ParsedError Build(Match m, bool hasColumn) =>
        new(m.Groups["file"].Value,
            int.Parse(m.Groups["line"].Value),
            hasColumn ? int.Parse(m.Groups["col"].Value) : 1,
            m.Groups["sev"].Value,
            m.Groups["msg"].Value);
}