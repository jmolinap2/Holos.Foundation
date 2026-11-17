# Versioned seeds

`VersionedSeedRunner<TScope, TSession>` validates an ordered catalog and delegates each step to
`IVersionedSeedStore<TScope, TSession>`. Scope is supplied by the product (tenant, company, or another
partition); Foundation does not impose a history table or identifiers from an application.

`RelationalVersionedSeedStore` provides a relational implementation template. Every step uses a
fresh context and a transaction, including any intermediate `SaveChanges` in the action. The
adapter must implement a transaction-owned database lock for the scope, read the existing history
after that lock, and add the history row through the same context. An in-process semaphore is not
sufficient for deployments with several processes. History still needs a unique scope/seed key.

The adapter factory must create independently owned contexts for the correct scope/database. This
implementation is deliberately not an adapter for an existing application unit of work. Do not
use it inside tenant provisioning until transaction ownership and rollback with the caller have
been explicitly resolved. Provider execution strategies may repeat an action after a transient
failure: seed actions must have no nontransactional side effects.

Use `ReadCommitted` with transaction-owned advisory locks on PostgreSQL so the history read sees a
previous runner's commit. Other providers may override isolation and locking together. Tests use
SQLite serializable immediate transactions with separate connections, not a simulated lock.
Those tests do not certify PostgreSQL or SQL Server adapters.

Version identifiers must never be reused. The runner does not make an arbitrary delegate
immutable: payloads must be frozen in product-owned versioned resources with change-detection
tests. Legacy data adoption is a product policy, not something Foundation can infer from counts.

No production application adapter is included here yet. No package was published by this work.
