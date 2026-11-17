using Abp.Application.Services.Dto;
using Abp.Domain.Entities;
using Holos.Foundation.Application;
using Holos.Foundation.Domain;

namespace Holos.Foundation.EntityFrameworkCore.Tests;

public class CompanyCrudAppServiceTests
{
    [Fact]
    public void Constructor_requires_a_company_access_guard()
    {
        Assert.Throws<ArgumentNullException>(() => new TestService(null!));
    }

    [Fact]
    public async Task Configured_guard_receives_the_requested_company_before_repository_access()
    {
        var guard = new RejectingGuard();
        var service = new TestService(guard);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(new TestDto { CompanyId = 42 }));
        Assert.Equal(42L, guard.RequestedCompanyId);
    }

    [Fact]
    public async Task Loading_uses_the_overridden_loader_after_authorizing_company()
    {
        var guard = new RecordingGuard();
        var service = new LoadingService(guard);
        var entity = await service.LoadAsync(42);
        Assert.True(guard.Called);
        Assert.Equal(42, entity.Id);
    }

    [Fact]
    public async Task Loading_converts_not_found_from_the_overridden_loader()
    {
        var service = new LoadingService(new RecordingGuard(), missing: true);
        var error = await Assert.ThrowsAsync<EntityNotFoundException>(() => service.LoadAsync(42));
        Assert.Equal(typeof(TestEntity), error.EntityType);
    }

    [Fact]
    public async Task Loading_does_not_reach_the_loader_when_company_access_is_denied()
    {
        var service = new TestService(new RejectingGuard());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoadAsync(42));
    }

    private sealed class RecordingGuard : ICompanyAccessGuard
    {
        public bool Called { get; private set; }
        public Task EnsureActiveCompanyAccessAsync(long? requestedCompanyId = null)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    private sealed class LoadingService(RecordingGuard guard, bool missing = false)
        : CompanyCrudAppService<TestEntity, TestDto, long, PagedResultRequestDto, TestDto, TestDto>(null!, guard)
    {
        public Task<TestEntity> LoadAsync(long id) => GetEntityByIdAsync(id);
        protected override Task<TestEntity> LoadEntityAsync(long id)
        {
            Assert.True(guard.Called);
            if (missing) throw new EntityNotFoundException(typeof(TestEntity), id);
            return Task.FromResult(new TestEntity { Id = id });
        }
    }

    private sealed class RejectingGuard : ICompanyAccessGuard
    {
        public long? RequestedCompanyId { get; private set; }

        public Task EnsureActiveCompanyAccessAsync(long? requestedCompanyId = null)
        {
            RequestedCompanyId = requestedCompanyId;
            throw new UnauthorizedAccessException();
        }
    }

    private sealed class TestService : CompanyCrudAppService<TestEntity, TestDto, long, PagedResultRequestDto, TestDto, TestDto>
    {
        // Las operaciones deben rechazar la llamada antes de consultar el repositorio.
        public TestService(ICompanyAccessGuard companyAccess) : base(null!, companyAccess) { }
        public Task<TestEntity> LoadAsync(long id) => GetEntityByIdAsync(id);
    }

    private sealed class TestEntity : Entity<long>, IMustHaveCompany
    {
        public long CompanyId { get; set; }
    }

    private sealed class TestDto : EntityDto<long>, IRequestsCompany
    {
        public long CompanyId { get; set; }
        public long? RequestedCompanyId => CompanyId;
    }
}
