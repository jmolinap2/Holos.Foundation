namespace Holos.Foundation.Domain.Seeding;

/// <summary>A version's action must use frozen inputs and perform no external side effects.</summary>
public sealed record VersionedSeed<TSession>(string Id, Func<TSession, CancellationToken, Task> Apply);

/// <summary>
/// El almacenamiento es quien controla la concurrencia de todo el ámbito y la transacción. Debe
/// volver a leer el historial después de adquirir el bloqueo y confirmar la acción y el historial
/// en conjunto. Si ocurre una falla, ninguno de los dos puede quedar confirmado. Una implementación
/// no debe reintentar efectos secundarios externos ni confirmar la transacción del llamador.
/// </summary>
public interface IVersionedSeedStore<TScope, TSession>
{
    Task ExecuteAtomicallyAsync(TScope scope, string seedId,
        Func<TSession, CancellationToken, Task> apply, CancellationToken cancellationToken);
}

/// <summary>Provider-independent ordered execution; history and concurrency belong to the storage adapter.</summary>
public sealed class VersionedSeedRunner<TScope, TSession>
{
    private readonly IVersionedSeedStore<TScope, TSession> _store;
    private readonly VersionedSeed<TSession>[] _steps;

    public VersionedSeedRunner(IVersionedSeedStore<TScope, TSession> store,
        IEnumerable<VersionedSeed<TSession>> steps)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(steps);
        _store = store;
        _steps = steps.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in _steps)
        {
            if (step is null || string.IsNullOrWhiteSpace(step.Id) || step.Apply is null)
                throw new ArgumentException("Each seed requires an identifier and an action.", nameof(steps));
            if (!ids.Add(step.Id))
                throw new ArgumentException($"Duplicate seed identifier: {step.Id}.", nameof(steps));
        }
    }

    public async Task RunAsync(TScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        foreach (var step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _store.ExecuteAtomicallyAsync(scope, step.Id, step.Apply, cancellationToken);
        }
    }
}
