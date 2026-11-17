namespace Holos.Foundation.Application;

/// <summary>
/// Comprueba que hay compañía activa y que el usuario puede operar en ella. La aplicación lo implementa una vez
/// (en HolosCoreOps, sobre <c>ICompanyContextGuard</c>) y las clases base lo llaman en lugar de cada método.
/// </summary>
public interface ICompanyAccessGuard
{
    /// <param name="requestedCompanyId">Compañía que pide la entrada, si pide alguna; si no coincide con la activa se rechaza.</param>
    Task EnsureActiveCompanyAccessAsync(long? requestedCompanyId = null);
}
