using System.Text.RegularExpressions;

namespace Holos.Foundation.Abp.Architecture;

/// <summary>Ningún mensaje de negocio debe escribirse como texto fijo: toda regla lleva una clave de localización.</summary>
public static class LiteralMessageGuard
{
    private static readonly Regex LiteralException = new(
        @"throw\s+new\s+(?:Abp\.UI\.)?UserFriendlyException\s*\(\s*\$?""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Ubicaciones <c>ruta:línea</c> de los <c>throw new UserFriendlyException("texto")</c>; ignora bin, obj y Migrations.</summary>
    public static IReadOnlyList<string> FindLiteralMessages(string sourceRoot)
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Contains("bin") || segments.Contains("obj") || segments.Contains("Migrations"))
                continue;

            var content = File.ReadAllText(file);
            foreach (Match match in LiteralException.Matches(content))
            {
                var line = content.AsSpan(0, match.Index).Count('\n') + 1;
                offenders.Add($"{relative.Replace('\\', '/')}:{line}");
            }
        }
        return offenders;
    }
}
