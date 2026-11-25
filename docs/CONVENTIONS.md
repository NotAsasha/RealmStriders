# Development conventions

## C# and Unity assets

- Keep gameplay decisions in testable C# classes where practical. Use
  `MonoBehaviour` for Unity lifecycle and presentation, and `NetworkBehaviour`
  for gameplay components that participate in NGO replication.
- Use strong types and explicit access modifiers. Comments should explain a
  decision or constraint that the code cannot express.
- Assign authored references through `[SerializeField]`. Avoid runtime
  fallbacks that search a hierarchy or guess components from object names.
- Cache components and cameras during initialization. Keep `GetComponent`,
  scene searches, `Camera.main`, LINQ and transient allocations out of frame
  updates and tight loops.
- Reuse the feature's interfaces and events. Preserve serialized references
  and asset metadata when moving or renaming assets. See the
  [Unity Editor workflow](../AGENTS.md#unity-editor).

## Verification

Compile changed C# in Unity and resolve type and namespace errors. Exercise
the affected feature from its normal entry point. For replicated gameplay,
check server authority, initial state and cleanup on a host and client.
Register network subscriptions in `OnNetworkSpawn` and remove them in
`OnNetworkDespawn`. See [Networking](NETWORKING.md).

## Documentation

Keep confirmed player-facing rules in [Game Design](GAME_DESIGN.md),
implementation ownership and known discrepancies in [Systems](SYSTEMS.md),
and repeatable authoring steps in the relevant specialist guide. Record an
unresolved design question as open until the owner decides it.

Link to the document that owns a rule. Keep the [README index](../README.md#documentation)
current when a guide moves. Leave completed-task logs, one-off benchmark
results and local test artifacts out of project guides. Required authoring
commands must be available in a fresh checkout.
