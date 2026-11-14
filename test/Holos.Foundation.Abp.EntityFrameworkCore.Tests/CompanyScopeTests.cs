using Holos.Foundation.Abp.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Holos.Foundation.Abp.EntityFrameworkCore.Tests;

public class CompanyScopeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CompanyScopeTests()
    {
        _connection.Open();
        using var seed = NewContext(filterEnabled: false, companyId: null);
        seed.Database.EnsureCreated();
        seed.Invoices.AddRange(new Invoice(1, "A-1"), new Invoice(1, "A-2"), new Invoice(2, "B-1"));
        seed.Notes.AddRange(new Note(null, "común"), new Note(1, "de uno"), new Note(2, "de dos"));
        seed.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private ScopedContext NewContext(bool filterEnabled, long? companyId) =>
        new(new DbContextOptionsBuilder<ScopedContext>().UseSqlite(_connection).Options)
        {
            FilterEnabled = filterEnabled,
            ActiveCompanyId = companyId,
        };

    [Fact]
    public void Query_only_returns_rows_of_the_active_company()
    {
        using var db = NewContext(true, 1);
        Assert.Equal(new[] { "A-1", "A-2" }, db.Invoices.OrderBy(x => x.Number).Select(x => x.Number).ToArray());
    }

    [Fact]
    public void The_same_model_filters_by_each_context_instance()
    {
        using var one = NewContext(true, 1);
        using var two = NewContext(true, 2);
        Assert.Equal(2, one.Invoices.Count());
        Assert.Equal(1, two.Invoices.Count());
    }

    [Fact]
    public void Without_active_company_the_filter_returns_nothing()
    {
        using var db = NewContext(true, null);
        Assert.Empty(db.Invoices.ToList());
    }

    [Fact]
    public void A_disabled_filter_returns_every_company()
    {
        using var db = NewContext(false, 1);
        Assert.Equal(3, db.Invoices.Count());
    }

    [Fact]
    public void Optional_company_rows_are_matched_by_equality()
    {
        using var db = NewContext(true, 1);
        Assert.Equal(new[] { "de uno" }, db.Notes.Select(x => x.Text).ToArray());

        using var common = NewContext(true, null);
        Assert.Equal(new[] { "común" }, common.Notes.Select(x => x.Text).ToArray());
    }

    [Fact]
    public void Insert_takes_the_active_company_when_none_is_set()
    {
        using var db = NewContext(true, 2);
        var invoice = new Invoice(0, "B-2");
        db.Invoices.Add(invoice);
        db.SaveChanges();

        Assert.Equal(2, invoice.CompanyId);
        Assert.Equal(2, db.Invoices.Count());
    }

    [Fact]
    public void Insert_for_another_company_is_rejected_while_the_filter_is_enabled()
    {
        using var db = NewContext(true, 1);
        db.Invoices.Add(new Invoice(2, "X"));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Moving_a_row_to_another_company_is_rejected_while_the_filter_is_enabled()
    {
        using var db = NewContext(true, 1);
        var invoice = db.Invoices.First();
        db.Entry(invoice).Property(x => x.CompanyId).CurrentValue = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Editing_other_fields_of_a_row_is_allowed()
    {
        using var db = NewContext(true, 1);
        var invoice = db.Invoices.First();
        db.Entry(invoice).Property(x => x.Number).CurrentValue = "A-9";
        db.SaveChanges();
        Assert.Equal(1, db.Invoices.Count(x => x.Number == "A-9"));
    }

    [Fact]
    public void Moving_a_row_to_another_company_is_allowed_when_the_filter_is_disabled()
    {
        using var db = NewContext(false, 1);
        var invoice = db.Invoices.First(x => x.CompanyId == 1);
        db.Entry(invoice).Property(x => x.CompanyId).CurrentValue = 2;
        db.SaveChanges();
        Assert.Equal(2, db.Invoices.Count(x => x.CompanyId == 2));
    }

    [Fact]
    public void Insert_for_another_company_is_allowed_when_the_filter_is_disabled()
    {
        using var db = NewContext(false, 1);
        db.Invoices.Add(new Invoice(2, "B-3"));
        db.SaveChanges();
        Assert.Equal(2, db.Invoices.Count(x => x.CompanyId == 2));
    }
}

public class Invoice : IMustHaveCompany
{
    public Invoice(long companyId, string number)
    {
        CompanyId = companyId;
        Number = number;
    }

    public int Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Number { get; private set; }
}

public class Note : IMayHaveCompany
{
    public Note(long? companyId, string text)
    {
        CompanyId = companyId;
        Text = text;
    }

    public int Id { get; private set; }
    public long? CompanyId { get; private set; }
    public string Text { get; private set; }
}

public class ScopedContext : DbContext, ICompanyFilterContext
{
    public ScopedContext(DbContextOptions<ScopedContext> options) : base(options) { }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Note> Notes => Set<Note>();

    public bool FilterEnabled { get; set; }
    public long? ActiveCompanyId { get; set; }

    bool ICompanyFilterContext.IsCompanyFilterEnabled => FilterEnabled;
    long? ICompanyFilterContext.CurrentCompanyId => ActiveCompanyId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>().HasQueryFilter(CompanyScope.CreateFilter<Invoice>(this));
        modelBuilder.Entity<Note>().HasQueryFilter(CompanyScope.CreateFilter<Note>(this));
    }

    public override int SaveChanges()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
            CompanyScope.ApplyOnAdded(this, entry);
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified).ToList())
            CompanyScope.ApplyOnModified(this, entry);
        return base.SaveChanges();
    }
}
