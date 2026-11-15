using Abp.Application.Services;
using Abp.Application.Services.Dto;
using Abp.Domain.Entities;
using Abp.Domain.Repositories;
using Holos.Foundation.Abp.Domain;

namespace Holos.Foundation.Abp.Application;

/// <summary>
/// CRUD de ABP para entidades de una compañía. Antes de cada operación valida el acceso a la compañía activa
/// (y la compañía que pida la entrada, si implementa <see cref="IRequestsCompany"/>); la pertenencia de las filas la
/// garantiza el filtro global de compañía, así que ya no hace falta resolver la compañía, comparar <c>CompanyId</c>
/// ni filtrar por ella en cada método. Una fila de otra compañía no se ve: se responde "no encontrada".
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

    /// <summary>Inyección por propiedad (ABP). Requiere registrar un guard de acceso en la aplicación.</summary>
    public ICompanyAccessGuard CompanyAccess { get; set; } = MissingCompanyAccessGuard.Instance;

    public override async Task<PagedResultDto<TEntityDto>> GetAllAsync(TGetAllInput input)
    {
        await CompanyAccess.EnsureActiveCompanyAccessAsync((input as IRequestsCompany)?.RequestedCompanyId);
        return await base.GetAllAsync(input);
    }

    public override async Task<TEntityDto> CreateAsync(TCreateInput input)
    {
        await CompanyAccess.EnsureActiveCompanyAccessAsync((input as IRequestsCompany)?.RequestedCompanyId);
        return await base.CreateAsync(input);
    }

    public override async Task<TEntityDto> UpdateAsync(TUpdateInput input)
    {
        await CompanyAccess.EnsureActiveCompanyAccessAsync((input as IRequestsCompany)?.RequestedCompanyId);
        return await base.UpdateAsync(input);
    }

    public override async Task DeleteAsync(EntityDto<TPrimaryKey> input)
    {
        CheckDeletePermission();
        // Por GetEntityByIdAsync (filtrado por compañía): borrar por Id sin cargar la fila alcanzaba filas de otra compañía.
        var entity = await GetEntityByIdAsync(input.Id);
        await Repository.DeleteAsync(entity);
    }

    protected override async Task<TEntity> GetEntityByIdAsync(TPrimaryKey id)
    {
        await CompanyAccess.EnsureActiveCompanyAccessAsync();
        try
        {
            return await base.GetEntityByIdAsync(id);
        }
        catch (EntityNotFoundException)
        {
            throw EntityNotFound(id);
        }
    }

    /// <summary>Error cuando la fila no existe o es de otra compañía. Por defecto, el de ABP (404).</summary>
    protected virtual Exception EntityNotFound(TPrimaryKey id) => new EntityNotFoundException(typeof(TEntity), id);

    private sealed class MissingCompanyAccessGuard : ICompanyAccessGuard
    {
        public static readonly MissingCompanyAccessGuard Instance = new();

        public Task EnsureActiveCompanyAccessAsync(long? requestedCompanyId = null) => throw new InvalidOperationException("Register an ICompanyAccessGuard before using company CRUD services.");
    }
}
