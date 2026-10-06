# Player Development Guidance

Read the root `AGENTS.md`, `docs/SYSTEMS.md`, `docs/NETWORKING.md`, and
`docs/GAME_DESIGN.md` before changing player or equipment code.

## Important entry points

- `Human.cs` — player-specific network state and death handling.
- `Entity.cs` — shared health, capture, and weakness state.
- `Inventory.cs` — configurable inventory slots and item ownership.
- `Equipment/Item.cs` — base item interaction contract.
- `Equipment/LeafBlower/LeafBlower.cs` — capture tool.
- `Equipment/` — equipment-specific effects and interactions.

- Equipment is part of the mission preparation and counterplay loop.
- Capture is performed by `LeafBlower`; other equipment should weaken, detect,
  or support unless the design explicitly changes.
- Combine existing equipment interfaces before adding a new interaction API.
- Validate inventory, money, capture, and damage outcomes on the authoritative
  peer.
- A dead player becomes a spectator until the mission ends.
