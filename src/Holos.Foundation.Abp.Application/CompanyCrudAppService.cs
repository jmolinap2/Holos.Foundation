using Abp.Application.Services;
using Abp.Application.Services.Dto;
using Abp.Domain.Entities;
using Abp.Domain.Repositories;
using Holos.Foundation.Abp.EntityFrameworkCore;

namespace Holos.Foundation.Abp.Application;

/// <summary>
/// CRUD de ABP para entidades de una compañía. Antes de cada operación valida el acceso a la compañía activa;
/// la pertenencia de las filas la garantiza el filtro global (<see cref="CompanyScope"/>), así que ya no hace falta
/// resolver la compañía, comparar <c>CompanyId</c> ni filtrar por ella en cada método.
/// Una fila de otra compañía no se ve: se responde "no encontrada", igual que si no existiera.
/// </summary>
public abstract class CompanyCrudAppService<TEntity, TEntityDto, TPrimaryKey, TGetAllInput, TCreateInput, TUpdateInput>
    : AsyncCrudAppService<TEntity, TEntityDto, TPrimaryKey, TGetAllInput, TCreateInput, TUpdateInput>
    where TEntity : class, IEntity<TPrimaryKey>, IMustHaveCompany
    where TEntityDto : IEntityDto<TPrimaryKey>
    where TUpdateInput : IEntityDto<TPrimaryKey>
{
    protected CompanyCrudAppService(IRepository<TEntity, TPrimaryKey> repository) : base(repository)
    {
    }

    /// <summary>Inyección por propiedad (ABP). Sin implementación registrada no se valida acceso.</summary>
    public ICompanyAccessGuard CompanyAccess { get; set; } = NullCompanyAccessGuard.Instance;

    public override async Task<PagedResultDto<TEntityDto>> GetAllAsync(TGetAllInput input)
    {
        await CompanyAccess.EnsureActiveCompanyAccessAsync();
        return await base.GetAllAsync(input);
    }

    public override async Task<TEntityDto> CreateAsync(TCreateInput input)
    {
        await CompanyAccess.EnsureActiveCompanyAccessAsync();
        return await base.CreateAsync(input);
    }

    public override async Task DeleteAsync(EntityDto<TPrimaryKey> input)
    {
        await CompanyAccess.EnsureActiveCompanyAccessAsync();
        await base.DeleteAsync(input);
    }

    protected override async Task<TEntity> GetEntityByIdAsync(TPrimaryKey id)
    {
        await CompanyAccess.EnsureActiveCompanyAccessAsync();
        return await base.GetEntityByIdAsync(id);
    }

    private sealed class NullCompanyAccessGuard : ICompanyAccessGuard
    {
        public static readonly NullCompanyAccessGuard Instance = new();

        public Task EnsureActiveCompanyAccessAsync() => Task.CompletedTask;
    }
}
