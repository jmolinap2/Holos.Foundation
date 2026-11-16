using System.Text.RegularExpressions;

namespace Holos.Foundation.Abp.Application;

/// <summary>
/// Propone el siguiente código libre con formato «PREFIJO-NNN» (por ejemplo <c>CAT-007</c>). Es pura: recibe los códigos
/// existentes del alcance (normalmente la compañía). Es una sugerencia editable; la unicidad se valida al guardar.
/// </summary>
public static class SequentialCodePolicy
{
    /// <summary>Ignora los códigos de otro formato y conserva el relleno con ceros de la serie existente.</summary>
    public static string SuggestNext(IEnumerable<string?> existingCodes, string prefix, int minimumDigits = 3)
    {
        if (string.IsNullOrEmpty(prefix))
            throw new ArgumentException("El prefijo del código es obligatorio.", nameof(prefix));

        var taken = new HashSet<string>(
            (existingCodes ?? Enumerable.Empty<string?>())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var pattern = new Regex("^" + Regex.Escape(prefix) + @"(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        long highest = 0;
        var digits = Math.Max(1, minimumDigits);
        foreach (var code in taken)
        {
            var match = pattern.Match(code);
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out var number))
                continue;

            highest = Math.Max(highest, number);
            digits = Math.Max(digits, match.Groups[1].Value.Length);
        }

        string candidate;
        do
        {
            highest++;
            candidate = prefix + highest.ToString().PadLeft(digits, '0');
        }
        while (taken.Contains(candidate));

        return candidate;
    }
}
