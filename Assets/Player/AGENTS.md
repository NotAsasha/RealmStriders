# Player and equipment

- [Human](Human.cs) handles player death and revival. [Entity](Entity.cs)
  owns shared health, effects, weakness and capture state.
- [Inventory](Inventory.cs) owns the replicated slots and selected item.
  [Item](Equipment/Item.cs) provides pickup, active-holder checks and
  equipment actions. Use the existing equipment interfaces when adding tools.
- [LeafBlower](Equipment/LeafBlower/LeafBlower.cs) handles capture.
  [Held items](../../docs/HELD_ITEMS.md) covers grip authoring and presentation.
- [CameraMovement](Movement/CameraMovement.cs) and
  [SpectatorController](Movement/SpectatorController.cs) hand the owner's
  view between gameplay and spectating. Exercise death, mission scene unload
  and revival together when changing this lifecycle.

Use [Networking](../../docs/NETWORKING.md) for authority checks and
[Game Design](../../docs/GAME_DESIGN.md) for the intended player rules.
