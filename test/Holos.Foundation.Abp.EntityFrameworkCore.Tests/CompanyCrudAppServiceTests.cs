using Abp.Application.Services.Dto;
using Abp.Domain.Entities;
using Holos.Foundation.Abp.Application;
using Holos.Foundation.Abp.Domain;

namespace Holos.Foundation.Abp.EntityFrameworkCore.Tests;

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
