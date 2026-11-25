# Base systems

[WorldChooser](WorldChooser/WorldChooser.cs) selects the mission and its danger
rolls. The [lever](Interactables/Lever/Lever.cs) submits start and return requests
to [GameManager](../GameManager.cs). Check both entry points when changing
mission flow, including a request from a remote terminal operator.

- [Shop](Shop/Shop.cs), [SellPoint](SellPoint/SellPoint.cs) and
  [BaseManager](BaseUpgrader/BaseManager.cs) own purchases, cage sales and
  upgrades. Resolve prices, shared funds and rewards on the server.
- [PortalManager](Portals/PortalManager.cs) controls the portal. Enemies can
  enter the base during an active mission.
- [Terminal](Terminal.cs) and [PackedFurnitureItem](PackedFurnitureItem.cs)
  handle packing and placement. For changes to furniture identity or
  persistence, check the [save instructions](../FileSystem/AGENTS.md).

See [Game Design](../../docs/GAME_DESIGN.md) for mission outcomes and
[Systems](../../docs/SYSTEMS.md#player-and-base-services) for station responsibilities.
