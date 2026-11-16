using Abp.Application.Services.Dto;
using Abp.Domain.Entities;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.UI;
using Holos.Foundation.Abp.Domain;

namespace Holos.Foundation.Abp.Application;

/// <summary>
/// CRUD de compañía para entidades con código de negocio único por compañía: valida la unicidad al crear y editar
/// y ofrece <see cref="SuggestCodeAsync"/> con el siguiente código libre de la serie.
/// </summary>
public abstract class CodedCompanyCrudAppService<TEntity, TEntityDto, TPrimaryKey, TGetAllInput, TCreateInput, TUpdateInput>
    : CompanyCrudAppService<TEntity, TEntityDto, TPrimaryKey, TGetAllInput, TCreateInput, TUpdateInput>
    where TEntity : class, IEntity<TPrimaryKey>, IMustHaveCompany, IHasCode
    where TEntityDto : IEntityDto<TPrimaryKey>
    where TCreateInput : IHasCode
    where TUpdateInput : IEntityDto<TPrimaryKey>, IHasCode
{
    protected CodedCompanyCrudAppService(
        IRepository<TEntity, TPrimaryKey> repository,
        ICompanyAccessGuard companyAccess) : base(repository, companyAccess)
    {
    }

    /// <summary>Prefijo de la serie sugerida, con su separador (por ejemplo <c>"CAJ-"</c>).</summary>
    protected abstract string CodePrefix { get; }

    /// <summary>Clave de localización del error de código repetido; recibe el código como <c>{0}</c>.</summary>
    protected abstract string DuplicateCodeMessageKey { get; }

    /// <summary>Siguiente código libre de la compañía activa (editable; la unicidad se valida al guardar).</summary>
    public virtual async Task<string> SuggestCodeAsync()
    {
        CheckCreatePermission();
        await CompanyAccess.EnsureActiveCompanyAccessAsync();
        // Incluye eliminados: el índice único también los cuenta.
        using var includeDeleted = CurrentUnitOfWork.DisableFilter(AbpDataFilters.SoftDelete);
        var codes = await AsyncQueryableExecuter.ToListAsync(Repository.GetAll().Select(x => x.Code));
        return SequentialCodePolicy.SuggestNext(codes, CodePrefix);
    }

    public override async Task<TEntityDto> CreateAsync(TCreateInput input)
    {
        CheckCreatePermission();
        await CompanyAccess.EnsureActiveCompanyAccessAsync((input as IRequestsCompany)?.RequestedCompanyId);
        await EnsureCodeIsUniqueAsync(input.Code, default, hasExcludedId: false);
        return await base.CreateAsync(input);
    }

    public override async Task<TEntityDto> UpdateAsync(TUpdateInput input)
    {
        CheckUpdatePermission();
        await CompanyAccess.EnsureActiveCompanyAccessAsync((input as IRequestsCompany)?.RequestedCompanyId);
        await EnsureCodeIsUniqueAsync(input.Code, input.Id, hasExcludedId: true);
        return await base.UpdateAsync(input);
    }

    private async Task EnsureCodeIsUniqueAsync(string code, TPrimaryKey? excludedId, bool hasExcludedId)
    {
        var ids = await AsyncQueryableExecuter.ToListAsync(Repository.GetAll().Where(x => x.Code == code).Select(x => x.Id));
        var duplicated = ids.Any(id => !hasExcludedId || !EqualityComparer<TPrimaryKey>.Default.Equals(id, excludedId!));
        if (duplicated)
            throw new UserFriendlyException(L(DuplicateCodeMessageKey, code));
    }
}
