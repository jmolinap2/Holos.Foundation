using Holos.Foundation.Domain.Seeding;

namespace Holos.Foundation.EntityFrameworkCore.Tests;

public class VersionedSeedRunnerTests
{
    [Fact]
    public async Task Orders_steps_and_leaves_history_decisions_to_store()
    {
        var store = new Store();
        var runner = Create(store);
        await runner.RunAsync(1);
        await runner.RunAsync(1);
        await runner.RunAsync(2);
        Assert.Equal(new[] { "first", "second" }, store.Values[1]);
        Assert.Equal(new[] { "first", "second" }, store.Values[2]);
    }

    [Fact]
    public async Task Failure_stops_following_steps_and_can_be_retried()
    {
        var store = new Store();
        var fail = true;
        var runner = new VersionedSeedRunner<int, List<string>>(store, new[]
        {
            new VersionedSeed<List<string>>("first", (session, _) =>
            {
                session.Add("first");
                if (fail) throw new InvalidOperationException();
                return Task.CompletedTask;
            }),
            Step("second")
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(1));
        Assert.Empty(store.History);
        Assert.False(store.Values.ContainsKey(1));
        fail = false;
        await runner.RunAsync(1);
        Assert.Equal(new[] { "first", "second" }, store.Values[1]);
    }

    [Fact]
    public async Task Cancellation_prevents_execution()
    {
        var store = new Store();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(store).RunAsync(1, cancelled.Token));
        Assert.Empty(store.History);
    }

    [Fact]
    public void Invalid_catalog_is_rejected_before_any_execution()
    {
        Assert.Throws<ArgumentException>(() => new VersionedSeedRunner<int, List<string>>(new Store(), new[] { Step("same"), Step("same") }));
        Assert.Throws<ArgumentException>(() => new VersionedSeedRunner<int, List<string>>(new Store(), new[] { Step(" ") }));
    }

    private static VersionedSeedRunner<int, List<string>> Create(Store store) => new(store, new[] { Step("first"), Step("second") });

    private static VersionedSeed<List<string>> Step(string id) => new(id, (session, _) =>
    {
        session.Add(id);
        return Task.CompletedTask;
    });

    // Transactional test double, not evidence of a production provider's transaction or lock behavior.
    private sealed class Store : IVersionedSeedStore<int, List<string>>
    {
        public readonly Dictionary<int, List<string>> Values = new();
        public readonly HashSet<(int Scope, string Id)> History = new();

        public async Task ExecuteAtomicallyAsync(int scope, string seedId, Func<List<string>, CancellationToken, Task> apply, CancellationToken cancellationToken)
        {
            if (History.Contains((scope, seedId))) return;
            var pending = Values.TryGetValue(scope, out var values) ? new List<string>(values) : new List<string>();
            await apply(pending, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Values[scope] = pending;
            History.Add((scope, seedId));
        }
    }
}
