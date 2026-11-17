using System.Linq.Expressions;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore;
using Abp.Events.Bus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Holos.Foundation.EntityFrameworkCore.Tests;

/// <summary>Contrato de la aplicación: de dónde sale la compañía activa (cabecera, claim, etc.).</summary>
public interface ICurrentCompany
{
    long? Id { get; }
}

/// <summary>
/// Cómo un <c>DbContext</c> de ABP usa la librería: tres métodos delegados y dos propiedades.
/// No se ejecuta (AbpDbContext necesita la infraestructura de ABP); su compilación confirma las firmas de ABP 10.3.
/// </summary>
public class ExampleAbpDbContext : AbpDbContext, ICompanyFilterContext
{
    public ExampleAbpDbContext(DbContextOptions<ExampleAbpDbContext> options) : base(options) { }

    public ICurrentCompany? CurrentCompany { get; set; } // inyección por propiedad de ABP

    bool ICompanyFilterContext.IsCompanyFilterEnabled =>
        CurrentUnitOfWorkProvider?.Current?.IsFilterEnabled(CompanyScope.FilterName) == true;

    long? ICompanyFilterContext.CurrentCompanyId => CurrentCompany?.Id;

    protected override bool ShouldFilterEntity<TEntity>(IMutableEntityType entityType) =>
        CompanyScope.Applies(entityType) || base.ShouldFilterEntity<TEntity>(entityType);

    protected override Expression<Func<TEntity, bool>>? CreateFilterExpression<TEntity>(ModelBuilder modelBuilder) =>
        CompanyScope.Combine(this, base.CreateFilterExpression<TEntity>(modelBuilder));

    protected override void ApplyAbpConceptsForAddedEntity(EntityEntry entry, long? userId, EntityChangeReport changeReport)
    {
        base.ApplyAbpConceptsForAddedEntity(entry, userId, changeReport);
        CompanyScope.ApplyOnAdded(this, entry);
    }
}

public static class ExampleModuleRegistration
{
    // En PreInitialize del módulo: Configuration.UnitOfWork.RegisterCompanyFilter();
    public static void Register(IUnitOfWorkDefaultOptions options) => options.RegisterCompanyFilter();
}
