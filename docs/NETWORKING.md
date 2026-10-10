# Networking

## Technology

- Netcode for GameObjects is used for replicated gameplay.
- Facepunch Steam transport is used for online connectivity.
- Network-aware gameplay components should inherit from `NetworkBehaviour`.

## Authority rules

The server owns shared gameplay outcomes. Clients may request actions, but the
server validates and applies the result.

Examples in the current code:

- `EnemySpawner.SpawnEnemies` exits unless `IsServer` is true.
- The shop receives a purchase request on the server, validates shared money,
  subtracts the price, and spawns the item.
- Network scene changes are initiated by the server.

Do not trust client-provided prices, rewards, damage, capture results, or
inventory changes.

## Host and save ownership

The game uses Steamworks lobbies with a player host. The host owns the game's
save file. If the host leaves, the networked game shuts down and the host can
later load the last save.

Clients detect a lost host through the local NGO disconnect callback, show
`host left the game`, and return to the main menu instead of remaining in a
stalled network session.

The save is written automatically after a mission completes. Manual saving is
allowed only while no mission is active.

If all living players disconnect and only a dead host remains, the mission
ends and all unreturned mission loot is lost.

The mission can also be ended by the base, by the mission timer, or by the
whole team dying. Only a mission with every target captured receives the
success rating increase.

## State versus events

- Use `NetworkVariable<T>` for persistent replicated state that late-joining
  clients need.
- Use RPCs for requests and transient events.
- Keep presentation-only effects local unless every client must observe them.
- Subscribe and unsubscribe to network lifecycle events in
  `OnNetworkSpawn`/`OnNetworkDespawn`.
- Noise that affects enemy behaviour is emitted and resolved on the server.
  Continuous equipment noise is driven by server-owned state, while voice
  transmission refreshes a short-lived server-side noise window.

## Adding a networked interaction

1. Decide which peer owns the authoritative state.
2. Define the client request and its server-side validation.
3. Apply the result only on the authoritative peer.
4. Replicate durable state with a `NetworkVariable` or a suitable networked
   object.
5. Use an RPC only for a transient notification or presentation event.
6. Handle despawn, scene changes, and late joining.

Mission scene objects brought into the base must be promoted to runtime network
objects before the mission scene is unloaded. Otherwise NGO removes them with
the scene even though they are inside the base.

## Known areas to verify

The network contract is not yet documented for every player equipment item,
save operation, and monster. When changing one of these areas, inspect the
existing implementation and update this document if the authority model
becomes clear.
