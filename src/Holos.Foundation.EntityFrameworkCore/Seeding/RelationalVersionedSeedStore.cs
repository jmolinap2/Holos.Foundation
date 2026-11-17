using System.Data;
using Holos.Foundation.Domain.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Holos.Foundation.EntityFrameworkCore.Seeding;

/// <summary>
/// Runs each step in a dedicated context and transaction, independently of caller units of work.
/// The factory must select the correct database for the scope. AcquireScopeLock must acquire a
/// database transaction-owned lock shared by all processes using that scope, before reading history.
/// </summary>
public abstract class RelationalVersionedSeedStore<TScope, TContext>(IDbContextFactory<TContext> factory)
    : IVersionedSeedStore<TScope, TContext> where TContext : DbContext
{
    protected virtual IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected abstract Task AcquireScopeLockAsync(TContext context, TScope scope, CancellationToken cancellationToken);
    protected abstract Task<bool> IsAppliedAsync(TContext context, TScope scope, string seedId, CancellationToken cancellationToken);
    protected abstract void RecordApplied(TContext context, TScope scope, string seedId);

    public async Task ExecuteAtomicallyAsync(TScope scope, string seedId,
        Func<TContext, CancellationToken, Task> apply, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(apply);
        if (System.Transactions.Transaction.Current != null)
            throw new NotSupportedException("Use an explicit adapter for the caller's ambient transaction.");
        await using var strategyContext = await factory.CreateDbContextAsync(cancellationToken);
        if (!strategyContext.Database.IsRelational())
            throw new NotSupportedException("Versioned seeds require a relational transaction provider.");

        var strategy = strategyContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A fresh tracker is essential after a rollback or a transient failure.
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel, cancellationToken);
            await AcquireScopeLockAsync(context, scope, cancellationToken);
            if (!await IsAppliedAsync(context, scope, seedId, cancellationToken))
            {
                await apply(context, cancellationToken);
                RecordApplied(context, scope, seedId);
                await context.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
