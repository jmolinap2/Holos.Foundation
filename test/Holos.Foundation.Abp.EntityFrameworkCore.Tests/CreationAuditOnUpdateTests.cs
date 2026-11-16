using Abp.Application.Services.Dto;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using Abp.ObjectMapping;
using Holos.Foundation.Abp.Application;
using Holos.Foundation.Abp.Domain;

namespace Holos.Foundation.Abp.EntityFrameworkCore.Tests;

public class CreationAuditOnUpdateTests
{
    [Fact]
    public void Editing_keeps_who_and_when_created_even_if_the_mapper_clears_them()
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var entity = new AuditedThing { Id = 1, CreatorUserId = 7, CreationTime = created };
        var service = new Service { ObjectMapper = new ClearingMapper() };

        service.Apply(new ThingDto { Id = 1 }, entity);

        Assert.Equal(7, entity.CreatorUserId);
        Assert.Equal(created, entity.CreationTime);
    }

    private sealed class Service : CompanyCrudAppService<AuditedThing, ThingDto, long, PagedResultRequestDto, ThingDto, ThingDto>
    {
        public Service() : base(null!, new NoGuard()) { }
        public void Apply(ThingDto input, AuditedThing entity) => MapToEntity(input, entity);
    }

    private sealed class NoGuard : ICompanyAccessGuard
    {
        public Task EnsureActiveCompanyAccessAsync(long? requestedCompanyId = null) => Task.CompletedTask;
    }

    /// <summary>Simula el mapeo de AutoMapper cuando el DTO no trae la auditoría: la deja en nulo.</summary>
    private sealed class ClearingMapper : IObjectMapper
    {
        public TDestination Map<TDestination>(object source) => throw new NotSupportedException();

        public IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source) => throw new NotSupportedException();

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            if (destination is AuditedThing thing)
            {
                thing.CreatorUserId = null;
                thing.CreationTime = default;
            }
            return destination;
        }
    }


    private sealed class AuditedThing : Entity<long>, IMustHaveCompany, ICreationAudited
    {
        public long CompanyId { get; set; }
        public long? CreatorUserId { get; set; }
        public DateTime CreationTime { get; set; }
    }

    private sealed class ThingDto : EntityDto<long> { }
}
