using System.Reflection;
using Holos.Foundation.Abp.Domain;

namespace Holos.Foundation.Abp.Architecture;

/// <summary>
/// El filtro global de compañía vive en un solo DbContext. ABP registra el repositorio de una entidad con el contexto que
/// declara su <c>DbSet</c>; si una entidad <see cref="IMustHaveCompany"/> no tiene <c>DbSet</c> en ese contexto, su
/// repositorio se resuelve con otro contexto sin filtro y las filas de otra compañía quedan visibles en silencio.
/// </summary>
public static class CompanyFilterGuard
{
    /// <summary>Nombres completos de las entidades con compañía que el contexto indicado no declara como <c>DbSet</c>.</summary>
    public static IReadOnlyList<string> FindEntitiesWithoutDbSet(Type filteredDbContext, IEnumerable<Assembly> domainAssemblies)
    {
        ArgumentNullException.ThrowIfNull(filteredDbContext);
        ArgumentNullException.ThrowIfNull(domainAssemblies);

        var declared = filteredDbContext.GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition().Name.StartsWith("DbSet", StringComparison.Ordinal))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToHashSet();

        return domainAssemblies
            .SelectMany(SafeTypes)
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IMustHaveCompany).IsAssignableFrom(t) && !declared.Contains(t))
            .Select(t => t.FullName ?? t.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>El ensamblado de partida y los que referencia que cumplan el filtro (por defecto los de sufijo <c>.Domain</c>).</summary>
    public static IReadOnlyList<Assembly> DiscoverAssemblies(Assembly start, Func<AssemblyName, bool>? include = null)
    {
        include ??= name => name.Name != null && name.Name.EndsWith(".Domain", StringComparison.Ordinal);
        var seen = new HashSet<string>();
        var pending = new Queue<Assembly>();
        var result = new List<Assembly>();
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            var assembly = pending.Dequeue();
            if (!seen.Add(assembly.FullName ?? assembly.GetName().Name!)) continue;
            result.Add(assembly);
            foreach (var reference in assembly.GetReferencedAssemblies().Where(include))
            {
                try { pending.Enqueue(Assembly.Load(reference)); }
                catch (Exception) { /* ensamblado opcional */ }
            }
        }
        return result;
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).Select(t => t!); }
    }
}
