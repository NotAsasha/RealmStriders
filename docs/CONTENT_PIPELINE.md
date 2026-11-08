# Content Pipeline

Read the relevant [design](GAME_DESIGN.md), [systems](SYSTEMS.md) and local
`AGENTS.md` before authoring. Use Unity MCP for scenes, prefabs and serialized
data. Keep assets paired with their `.meta` files.

## Monsters

1. Define role, danger, detection, attacks, equipment counterplay and failure
   cases in [Enemies](ENEMIES.md). Every target must be capturable by LeafBlower;
   killing is not a mission objective.
2. Reuse shared enemy abstractions and implement specialized behaviour in the
   monster's folder. Identify stackable weaknesses and server-owned state.
3. Configure the prefab, navigation, animation, effects and separate step/vocal
   sources. Register it in NGO and the appropriate enemy danger pool.
4. Configure explicit EnemyAcoustics emitter pairs as described in
   [Steam Audio](STEAM_AUDIO.md#вороги-та-нові-джерела).
5. Test host/client spawn, capture, player death and mission-end despawn.

## Equipment

1. Define whether it captures, weakens, detects or supports, and which enemies
   it affects. Reuse existing item/equipment interfaces.
2. Validate requests on the server before applying gameplay effects. Configure
   the network prefab and saveable registration where applicable.
3. Assign visuals, effects and the `Item.icon` inventory sprite. If `clickSounds`
   is populated, assign an enabled AudioSource on the Item root with Play On
   Awake disabled; a child source does not supply Item's default use click.
4. Set GripPoint at the physical handle, then author held poses and root-space
   `heldCameraBounds` from physical meshes, excluding effect volumes. Assign
   all inventory presentation arrays, including children. Never hide a network
   root with `SetActive(false)`. See [Held items](HELD_ITEMS.md).
5. Test stacking/capture timing, charge if applicable, pickup/selection/drop,
   stored presentation, death, late joining and mission return/cleanup.

Preserve existing `NetworkSaveables` registry indices: save files use them as
prefab IDs. New variants need distinct registered identities.

## Shop previews

Item cards use mesh-only prefabs under `Assets/Base/Shop/Previews/`. Assign Shop,
catalog, Buy button, shared ShopItemPreview, preview RawImage and model explicitly.
Set the category's required rating to 0, 4, 7, 10 or 13. Server purchases use the
card's configured price and requirement.

After a mesh change, update the preview with centered/scaled mesh renderers and
materials. Bake ProBuilder geometry if needed. Exclude gameplay scripts,
particles, colliders, lights and NetworkObjects. Verify detail and quick-buy
flows, preview shutdown and the locked-category state.

## Crafting recipes

Add an entry to `Crafter.Recipes` with two input Item prefabs and one output
prefab. Input order does not matter. Configure `Craft Text` for the preview;
empty text falls back to input/output names. Upgraded variants must retain
distinct identities; do not match ingredients by display name.

Dynamic inputs use NGO prefab hashes. Scene inputs resolve `Item.PrefabId`
through `NetworkItemsHandler.database`; missing registry data rejects them.
Assign socket triggers, output point, doors, indicators, audio, particles and
preview labels explicitly. Connect the physical button to `TryStartCraft`.
Check both ingredient orders, wrong variants, pickup from an occupied socket,
output spawn and consistent host/client preview/effects.

## Worlds and missions

1. Define the objective and return outcome within the confirmed mission rules.
2. Configure the scene, spawn references, portal, navigation and mission selection;
   add the scene to the enabled build list.
3. Verify server additive loading, enemy placement outside excluded portal/base
   areas and unloading with a host and a client.
4. Test capture/return, early closure, timer expiry, team death and disconnects,
   including both returned and unreturned held/stored items.
5. For acoustic geometry, probes and rebaking follow
   [Steam Audio](STEAM_AUDIO.md#геометрія-та-повторне-запікання). Review only
   structural meshes; decor/furniture stay outside the acoustic export.
6. Casino art changes also follow [Casino art](CASINO_ART.md).

Content is complete when implementation, authored references, authority,
player-facing counterplay and documentation agree. Record any remaining design
or implementation gap explicitly.
