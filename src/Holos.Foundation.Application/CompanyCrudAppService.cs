using Abp.Application.Services;
using Abp.Application.Services.Dto;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using Abp.Timing;
using Abp.Domain.Repositories;
using Holos.Foundation.Domain;

namespace Holos.Foundation.Application;

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
    protected CompanyCrudAppService(
        IRepository<TEntity, TPrimaryKey> repository,
        ICompanyAccessGuard companyAccess) : base(repository)
    {
        CompanyAccess = companyAccess ?? throw new ArgumentNullException(nameof(companyAccess));
    }

    /// <summary>Guard registrado por la aplicación que resuelve y autoriza la compañía activa.</summary>
    protected ICompanyAccessGuard CompanyAccess { get; }

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
            return await LoadEntityAsync(id);
        }
        catch (EntityNotFoundException)
        {
            throw EntityNotFound(id);
        }
    }

    /// <summary>
    /// Al editar, la creación (quién y cuándo) no la decide la entrada: si el DTO no la trae, el mapeo la dejaba en nulo.
    /// </summary>
    protected override void MapToEntity(TUpdateInput updateInput, TEntity entity)
    {
        var creator = (entity as ICreationAudited)?.CreatorUserId;
        var createdAt = (entity as IHasCreationTime)?.CreationTime;
        base.MapToEntity(updateInput, entity);

        if (entity is ICreationAudited audited && audited.CreatorUserId != creator)
            audited.CreatorUserId = creator;
        if (entity is IHasCreationTime timed && createdAt.HasValue && timed.CreationTime != createdAt.Value)
            timed.CreationTime = createdAt.Value;
    }

    /// <summary>Carga la entidad tras validar el acceso; permite incluir relaciones necesarias para el CRUD.</summary>
    protected virtual Task<TEntity> LoadEntityAsync(TPrimaryKey id) => base.GetEntityByIdAsync(id);

    /// <summary>Error cuando la fila no existe o es de otra compañía. Por defecto, el de ABP (404).</summary>
    protected virtual Exception EntityNotFound(TPrimaryKey id) => new EntityNotFoundException(typeof(TEntity), id);

}
