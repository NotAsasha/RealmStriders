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
- Rely on serialized fields (`[SerializeField]`) assigned in the Inspector; do not write runtime fallback search heuristics (e.g. searching child names with `GetComponentsInChildren` / `.name.Contains(...)`).

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

Keep each rule or workflow in one owning document and link to it from others.
Game Design owns confirmed intent; Systems records implementation gaps without
turning them into new rules. Update the README index when adding/removing docs.

Do not retain completed task diaries, obsolete alternatives, fixed scene counts
or benchmark snapshots as current guidance. Document repeatable validation and
its limits. Local helpers/reports under the ignored `tools/` directory are not
prerequisites for a fresh checkout; durable authoring commands belong in the
versioned project.
