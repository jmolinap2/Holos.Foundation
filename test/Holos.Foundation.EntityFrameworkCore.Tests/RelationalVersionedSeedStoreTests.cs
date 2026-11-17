using System.Data;
using Holos.Foundation.Domain.Seeding;
using Holos.Foundation.EntityFrameworkCore.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Holos.Foundation.EntityFrameworkCore.Tests;

public class RelationalVersionedSeedStoreTests
{
    [Fact]
    public async Task Separate_connections_serialize_competing_runners_before_reading_history()
    {
        var file = Path.Combine(Path.GetTempPath(), $"holos-seed-{Guid.NewGuid():N}.db");
        try
        {
            var factory = new Factory(new DbContextOptionsBuilder<SeedContext>()
                .UseSqlite($"Data Source={file};Pooling=False;Default Timeout=10").Options);
            await using (var setup = factory.CreateDbContext()) await setup.Database.EnsureCreatedAsync();
            var calls = 0;
            var steps = new[] { new VersionedSeed<SeedContext>("one", async (db, ct) =>
            {
                Interlocked.Increment(ref calls);
                await Task.Delay(50, ct);
                db.Values.Add(new Value { Scope = 1, Text = "created" });
            }) };
            var first = new VersionedSeedRunner<int, SeedContext>(new Store(factory), steps);
            var second = new VersionedSeedRunner<int, SeedContext>(new Store(factory), steps);
            await Task.WhenAll(Task.Run(() => first.RunAsync(1)), Task.Run(() => second.RunAsync(1)));
            Assert.Equal(1, calls);
            await using var check = factory.CreateDbContext();
            Assert.Single(await check.Values.ToListAsync());
            Assert.Single(await check.History.ToListAsync());
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task Rollback_includes_early_save_and_history_and_retry_uses_a_fresh_context()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var factory = new Factory(new DbContextOptionsBuilder<SeedContext>().UseSqlite(connection).Options);
        await using (var setup = factory.CreateDbContext()) await setup.Database.EnsureCreatedAsync();
        var fail = true;
        var runner = new VersionedSeedRunner<int, SeedContext>(new Store(factory), new[]
        {
            new VersionedSeed<SeedContext>("one", async (db, ct) =>
            {
                db.Values.Add(new Value { Scope = 1, Text = "created" });
                await db.SaveChangesAsync(ct);
                if (fail) throw new InvalidOperationException("Injected after SaveChanges");
            })
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(1));
        await using (var check = factory.CreateDbContext())
        {
            Assert.Empty(await check.Values.ToListAsync());
            Assert.Empty(await check.History.ToListAsync());
        }
        fail = false;
        await runner.RunAsync(1);
        await runner.RunAsync(1);
        await using var final = factory.CreateDbContext();
        Assert.Single(await final.Values.ToListAsync());
        Assert.Single(await final.History.ToListAsync());
    }

    private sealed class Factory(DbContextOptions<SeedContext> options) : IDbContextFactory<SeedContext>
    {
        public SeedContext CreateDbContext() => new(options);
    }

    private sealed class Store(Factory factory) : RelationalVersionedSeedStore<int, SeedContext>(factory)
    {
        protected override IsolationLevel IsolationLevel => IsolationLevel.Serializable;
        // SQLite's immediate write transaction serializes writers before the history read.
        protected override Task AcquireScopeLockAsync(SeedContext db, int scope, CancellationToken ct) => Task.CompletedTask;
        protected override Task<bool> IsAppliedAsync(SeedContext db, int scope, string id, CancellationToken ct) =>
            db.History.AnyAsync(x => x.Scope == scope && x.SeedId == id, ct);
        protected override void RecordApplied(SeedContext db, int scope, string id) => db.History.Add(new History { Scope = scope, SeedId = id });
    }

    private sealed class SeedContext(DbContextOptions<SeedContext> options) : DbContext(options)
    {
        public DbSet<Value> Values => Set<Value>();
        public DbSet<History> History => Set<History>();
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<History>().HasKey(x => new { x.Scope, x.SeedId });
    }
    private sealed class Value
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public string Text { get; set; } = "";
    }
    private sealed class History
    {
        public int Scope { get; set; }
        public string SeedId { get; set; } = "";
    }
}
