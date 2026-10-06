# Base Development Guidance

Read the root `AGENTS.md`, `docs/GAME_DESIGN.md`, and `docs/SYSTEMS.md` before
changing base systems.

## Important entry points

- `WorldChooser/WorldChooser.cs` — mission list and danger roll selection.
- `Shop/Shop.cs` — server-side purchases and item spawning.
- `SellPoint/SellPoint.cs` — selling items for team money.
- `Portals/PortalManager.cs` — portal state and mission access.
- `BaseUpgrader/BaseManager.cs` — base progression and upgrades.
- `../GameManager.cs` — mission transitions and team rating.

- The base is the preparation, selling, shopping, upgrade, and mission
  transition area.
- Captured `EnemyCage` items are sold for money after a successful return.
- Team rating increases by a fixed amount for a successful mission and
  unlocks shop content and harder enemies.
- A mission may be ended from the base at any time, but it only grants the
  rating increase when all targets were captured.
- The base is not guaranteed to be safe after a portal is opened.
- Shared money, rating, purchases, and upgrades must be authoritative.
- Preserve both local and networked scene-flow behaviour.
