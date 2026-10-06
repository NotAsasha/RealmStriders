# Development Conventions

## Before editing

1. Read the applicable `AGENTS.md` files.
2. Read the relevant system document in `docs/`.
3. Inspect the target script, prefab, and nearby interfaces.
4. Check whether the feature is local, networked, persistent, or a mixture.

## Unity assets

- Keep `.meta` files paired with Unity assets.
- Prefer editing existing prefabs and scripts over duplicating equivalent
  assets.
- Do not manually edit generated folders such as `Library/`, `Temp/`, `obj/`,
  or `Builds/`.
- When creating a Unity asset, create it through Unity or preserve the
  corresponding metadata.

## C# and gameplay code

- Use explicit access modifiers and strong types.
- Keep gameplay rules out of presentation code where practical.
- Cache component references outside frame loops.
- Avoid allocations, LINQ, and repeated lookups in hot update paths.
- Reuse existing interfaces and events before adding new communication paths.

## Networked code

- Follow the authority rules in `docs/NETWORKING.md`.
- Validate all client requests on the server.
- Do not use a client-side visual effect as proof that a gameplay action
  succeeded.

## Documentation

Update the relevant document when a change affects:

- a gameplay rule or player-facing loop;
- system ownership or dependencies;
- network authority or replicated state;
- how to add a monster, item, or other content.

Use `TODO` for an unresolved design decision rather than guessing.
