# Asset Development Guidance

The `Assets/` folder contains the Unity game code, scenes, prefabs, and
content. Read the root `AGENTS.md` and the relevant document in `docs/` before
making gameplay changes.

## Choose the local guidance

- Enemy changes: read `docs/ENEMIES.md`.
- Player or equipment changes: read `docs/SYSTEMS.md` and
  `docs/NETWORKING.md`.
- Base, shop, upgrade, or portal changes: read `docs/SYSTEMS.md`.
- Save/load changes: read `docs/SYSTEMS.md` and verify whether the state is
  persistent by design.

## Important entry points

- `GameManager.cs` — mission lifecycle, timer, team rating, and scene flow.
- `Player/` — player entity, movement, inventory, equipment, and UI.
- `Enemy/` — shared enemy lifecycle, spawning, and monster-specific behaviour.
- `Base/` — shop, upgrades, portals, selling, and mission selection.
- `FileSystem/` — save files and persistence helpers.
- `Steam/` and `Plugins/Facepunch/` — lobby and transport integration.

## Unity assets

Keep Unity `.meta` files synchronized with their assets. Check prefabs and
scene references when changing serialized fields or component names.

When inspecting scenes, prefabs, GameObjects, components, or serialized fields,
use the Unity MCP server before parsing Unity YAML or `.meta` files manually.
Use direct file tools for C# source and documentation; use Unity MCP for the
actual imported Unity object state.
