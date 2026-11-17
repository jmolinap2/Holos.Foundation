using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Holos.Foundation.Architecture;

/// <summary>
/// Paridad de los diccionarios XML de localización de ABP: toda clave existe en todos los idiomas, ninguna se repite y los
/// marcadores {0}, {1}... de cada traducción coinciden con los del idioma por defecto.
/// </summary>
public static class LocalizationDictionaryGuard
{
    private static readonly Regex Placeholder = new(@"\{\d+(?::[^}]*)?\}", RegexOptions.Compiled);

    /// <summary>Carga cada archivo como clave→texto (la primera definición gana si una clave se repite).</summary>
    public static IReadOnlyDictionary<string, Dictionary<string, string>> Load(string directory, IEnumerable<string> files) =>
        files.ToDictionary(
            file => file,
            file => XDocument.Load(Path.Combine(directory, file))
                .Descendants("text")
                .Where(text => !string.IsNullOrWhiteSpace(text.Attribute("name")?.Value))
                .GroupBy(text => text.Attribute("name")!.Value, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Attribute("value")?.Value ?? g.First().Value, StringComparer.Ordinal));

    /// <summary>Por archivo, las claves vigentes (las de los archivos de referencia) que no tiene.</summary>
    public static IReadOnlyList<string> FindMissingKeys(
        IReadOnlyDictionary<string, Dictionary<string, string>> dictionaries, params string[] referenceFiles)
    {
        var current = referenceFiles.SelectMany(f => dictionaries[f].Keys).ToHashSet(StringComparer.Ordinal);
        return dictionaries
            .Select(d => (d.Key, Missing: current.Where(k => !d.Value.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList()))
            .Where(x => x.Missing.Count > 0)
            .Select(x => $"{x.Key} no tiene {x.Missing.Count} claves: {string.Join(", ", x.Missing.Take(15))}")
            .ToList();
    }

    /// <summary>Por archivo, las claves definidas más de una vez.</summary>
    public static IReadOnlyList<string> FindDuplicatedKeys(string directory, IEnumerable<string> files)
    {
        var problems = new List<string>();
        foreach (var file in files)
        {
            var duplicated = XDocument.Load(Path.Combine(directory, file))
                .Descendants("text")
                .Select(text => text.Attribute("name")?.Value)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name!, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicated.Count > 0)
                problems.Add($"{file} repite claves: {string.Join(", ", duplicated.Take(15))}");
        }
        return problems;
    }

    /// <summary>Por archivo, las claves cuyos marcadores {n} difieren de los del archivo por defecto.</summary>
    public static IReadOnlyList<string> FindPlaceholderMismatches(
        IReadOnlyDictionary<string, Dictionary<string, string>> dictionaries, string defaultFile)
    {
        var problems = new List<string>();
        var reference = dictionaries[defaultFile];
        foreach (var (file, texts) in dictionaries.Where(d => d.Key != defaultFile))
        {
            var mismatched = reference
                .Where(pair => texts.TryGetValue(pair.Key, out var value) && Markers(value) != Markers(pair.Value))
                .Select(pair => pair.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
            if (mismatched.Count > 0)
                problems.Add($"{file} cambia los marcadores {{n}} de {mismatched.Count} claves: {string.Join(", ", mismatched.Take(15))}");
        }
        return problems;
    }

    private static string Markers(string? text) =>
        string.Join("|", Placeholder.Matches(text ?? string.Empty).Select(m => m.Value).Distinct().OrderBy(m => m, StringComparer.Ordinal));
}
