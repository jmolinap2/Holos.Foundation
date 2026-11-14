namespace Holos.Foundation.Abp.Application;

/// <summary>
/// Comprueba que hay compañía activa y que el usuario puede operar en ella. La aplicación lo implementa una vez
/// (en HolosCoreOps, sobre <c>ICompanyContextGuard</c>) y las clases base lo llaman en lugar de cada método.
/// </summary>
public interface ICompanyAccessGuard
{
    Task EnsureActiveCompanyAccessAsync();
}
