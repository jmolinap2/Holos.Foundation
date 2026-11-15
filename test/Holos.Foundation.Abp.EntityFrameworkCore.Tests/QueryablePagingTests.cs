using Abp.Application.Services.Dto;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Holos.Foundation.Abp.EntityFrameworkCore.Tests;

public class QueryablePagingTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public QueryablePagingTests()
    {
        _connection.Open();
        using var seed = NewContext();
        seed.Database.EnsureCreated();
        for (var i = 1; i <= 25; i++)
            seed.Invoices.Add(new Invoice(1, $"N-{i:00}"));
        seed.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private ScopedContext NewContext() =>
        new(new DbContextOptionsBuilder<ScopedContext>().UseSqlite(_connection).Options) { FilterEnabled = false };

    private static PagedResultRequestDto Request(int skip, int take) => new() { SkipCount = skip, MaxResultCount = take };

    [Fact]
    public async Task Returns_the_requested_page_and_the_total()
    {
        using var db = NewContext();
        var page = await db.Invoices.OrderBy(x => x.Number).ToPagedResultAsync(Request(10, 5), x => x.Number);

        Assert.Equal(25, page.TotalCount);
        Assert.Equal(new[] { "N-11", "N-12", "N-13", "N-14", "N-15" }, page.Items);
    }

    [Fact]
    public async Task Incomplete_first_page_gives_the_total_without_counting()
    {
        using var db = NewContext();
        var page = await db.Invoices.Where(x => x.Number.StartsWith("N-0")).OrderBy(x => x.Number)
            .ToPagedResultAsync(Request(0, 50), x => x.Number);

        Assert.Equal(9, page.TotalCount);
        Assert.Equal(9, page.Items.Count);
    }

    [Fact]
    public async Task Full_first_page_still_reports_the_real_total()
    {
        using var db = NewContext();
        var page = await db.Invoices.OrderBy(x => x.Number).ToPagedResultAsync(Request(0, 10), x => x.Number);

        Assert.Equal(25, page.TotalCount);
        Assert.Equal(10, page.Items.Count);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_but_keeps_the_total()
    {
        using var db = NewContext();
        var page = await db.Invoices.OrderBy(x => x.Number).ToPagedResultAsync(Request(100, 10), x => x.Number);

        Assert.Equal(25, page.TotalCount);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task An_empty_result_has_total_zero()
    {
        using var db = NewContext();
        var page = await db.Invoices.Where(x => x.Number == "no existe").OrderBy(x => x.Number)
            .ToPagedResultAsync(Request(0, 10), x => x.Number);

        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Rows_are_read_without_change_tracking()
    {
        using var db = NewContext();
        await db.Invoices.OrderBy(x => x.Number).ToPagedResultAsync(Request(0, 5), x => x.Number);

        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Projection_pages_in_sql_and_matches_the_mapped_version()
    {
        using var db = NewContext();
        var mapped = await db.Invoices.OrderBy(x => x.Number).ToPagedResultAsync(Request(5, 7), x => x.Number);
        var projected = await db.Invoices.OrderBy(x => x.Number).ToPagedProjectionAsync(Request(5, 7), x => x.Number);

        Assert.Equal(mapped.TotalCount, projected.TotalCount);
        Assert.Equal(mapped.Items, projected.Items);
    }

    [Fact]
    public async Task Negative_skip_is_treated_as_zero()
    {
        using var db = NewContext();
        var page = await db.Invoices.OrderBy(x => x.Number).ToPagedResultAsync(Request(-5, 3), x => x.Number);

        Assert.Equal(new[] { "N-01", "N-02", "N-03" }, page.Items);
    }
}
