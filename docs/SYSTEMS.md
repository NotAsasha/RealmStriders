# Systems Overview

This document describes the current code organization. It is an orientation
map for contributors and agents, not a replacement for the source of truth in
the implementation.

## System map

| System | Main area | Responsibility |
| --- | --- | --- |
| Game flow | `Assets/GameManager.cs`, `Assets/Scenes/` | Shared game state, mission transitions, scene flow |
| Player | `Assets/Player/` | Player entity, movement, four-slot inventory, equipment, UI, voice |
| Enemies | `Assets/Enemy/` | Shared enemy behaviour, spawning, detection, and monster-specific logic |
| Base | `Assets/Base/` | Shop, upgrades, power, portals, radar, selling, and base interactions |
| Persistence | `Assets/FileSystem/` | Save files and load/delete operations, including local settings |
| Steam/network bootstrap | `Assets/Steam/`, `Assets/Plugins/Facepunch/` | Steam startup, lobbies, transport, and network setup |
| Scenes | `Assets/Scenes/` | Menu, base, mission, end, and world scene assets |
| Shared effects | `Assets/Resources/`, `Assets/Scripts/`, `Assets/Shaders/` | Reusable presentation and rendering effects |

## Authority and state ownership

The current project uses server-authoritative networking for important shared
operations. In particular:

- Enemy spawning is server-only.
- Purchasing is requested through an RPC and changes shared team money on the
  server.
- Network scene loading is initiated by the server.
- Persistent save behaviour is handled by the file-system classes and should
  not be duplicated inside gameplay components.
- `SettingsFile` owns local settings such as input bindings, mouse sensitivity,
  and master volume. Master volume is applied through `AudioListener.volume`,
  so it affects every `AudioSource` in the active scene.
- Graphics settings are also local and are applied through Unity's
  `QualitySettings`, `Screen.SetResolution`, `Screen.fullScreenMode`,
  VSync, frame-rate, and shadow-quality APIs. Post-processing is intentionally
  not toggled yet; its control can be added without changing the save boundary.
- The shared Settings prefab presents horizontal Graphics, Sound, and Controls
  tabs. Panel, button, and active-indicator references are serialized in
  SettingsMenu; category buttons call SelectCategory with their index.
  Opening the menu selects Graphics, and switching tabs preserves pending values.
  Apply saves all categories; closing discards unapplied changes. Master volume
  previews immediately over the full 0–100% range.
- The host owns the save file; automatic saves happen after mission
  completion, while manual saves are allowed only outside active missions.
- Mission-zone objects are transient; objects left outside the base are
  destroyed when the mission ends, while objects brought into the base remain.
- Items that fall below the lobby are caught by an independent return zone and
  restored to the shop tube's configured spawn point on the server.
- Mission scene objects brought into the base are promoted to runtime network
  objects before the mission scene is unloaded, so they survive scene cleanup.

When adding a networked feature, identify its authoritative owner before
adding a `NetworkVariable`, RPC, or local cache.

## Scene flow

The Menu scene uses left-aligned Host Lobby, Join Lobby, Settings, and Exit
navigation managed by LobbyUiManager with serialized page references. Host Lobby
opens the existing save selection and host flow. Join Lobby is a UI placeholder:
public Steam lobby discovery and joining are not implemented yet. Exit quits the
standalone application and stops Play Mode in the Editor. Main, Host, Join, and
Settings follow the owner-authored Base and SaveSelector style: translucent dark
backdrops, Cinzel Decorative headings, Syncopate buttons, Unica One technical
labels, and Smooch Sans descriptions. File management dialogs use an opaque dark
backdrop for readability and retain their separate confirmation step.
Host Lobby requires an explicit save-slot selection on each menu session.
The selected card is highlighted and labelled SELECTED; Create Lobby is disabled
until selection. LobbyUiManager validates the selection before starting the host,
assigns GameManager.currentSave, and runs scene/UI continuation only after NGO
reports that the host started. Save reset is behind Manage Files and a separate
confirmation dialog; cancel is the default focused action. Reset includes local
settings as well as save slots, preserves the existing file reset behavior, and
clears the selected slot.
Page navigation crossfades over 0.32 seconds with a subtle title slide, using
unscaled time. Serialized CanvasGroups block input during transitions.
The Menu scene routes Settings' close event through LobbyUiManager so its
unapplied settings are reverted only after the outgoing page finishes fading.

The project has bootstrap, menu, lobby/base, mission, and end scenes. Local
scene loading uses Unity's `SceneManager`; networked scene loading is
requested from the server through the NGO scene manager. A feature that
changes scene flow must account for both local and networked paths.

The mission can end from the base at any time, when its timer expires, or when
all players are dead. A complete capture set is required for the rating
increase.

## Dependency rules

- Keep gameplay rules separate from presentation where practical.
- Reuse existing interfaces in `Assets/Player/Equipment/` and `Assets/Base/`
  before introducing parallel interaction APIs.
- Check nearby `.asmdef` files before creating cross-assembly references.
- Do not put transient mission state into save files unless the design
  explicitly requires persistence.

## Adding a system

Before adding a new system:

1. Identify the owning scene and authority.
2. Find an existing system with a similar responsibility.
3. Define the public interaction or event boundary.
4. Decide whether the state is transient, networked, or persistent.
5. Add the smallest integration test or manual verification path available.

## Pause and shop presentation

Pause uses the shared Settings prefab. Its close event returns to the crew panel;
Resume restores gameplay input. Graphite surfaces and the existing menu fonts
are shared with the main menu.
Pause opens and closes through a shared CanvasGroup opacity fade (0.24s in,
0.16s out), including Settings when closing. The transition uses unscaled
time, reverses from its current opacity on repeated Escape, and disables
menu interaction until the opening fade completes. The pause prefab no
longer uses the screen-shatter effect.

Shop has five star categories with rating thresholds 0, 4, 7, 10 and 13.
The first category remains available at startup; later categories unlock at
the first segment of their star. Existing item groupings and prices are kept.
Categories four and five currently show an empty state. CategorySwitch owns
page selection and hides an open item card when changing categories.
Catalog tiles open item details; a separate BUY button beside each numeric
price purchases directly without opening details. Shop updates quick-buy
availability from money and rating changes during its network lifetime.
Details retain their preview and purchase flow, with BACK left and BUY right.

ItemCard holds its configured price, rating requirement and preview references.
The server validates purchases against those catalog values, including the
rating requirement; the client-supplied price is not used for charging.
ShopItemPreview renders mesh-only assets in a separate local scene, rotates
them with unscaled time and fades them in. Its camera stops rendering when
the card closes or the local player leaves the terminal. No gameplay item,
collider or NetworkObject is instantiated for the preview.

## UI audio

UI controls share local click feedback through `Game.UI.UIControlSound`.
Button clicks use the first persistent callback so navigation cannot cut off
the sound; non-button controls play on press/submit. Disabled controls stay
silent; hovering and keyboard focus changes are also silent. `Resources/UIAudio`
provides the configured clips and two 2D sources, persists across scenes,
and respects master volume while remaining audible during listener pause.
`tools/InstallUIAudio.cs` wires and verifies controls in game prefabs and
scenes, including templates for dynamically created cards and dropdown items.

## Base upgrade presentation

BaseUpgrader presents all six existing upgrades in a graphite card grid using
the menu fonts. Purchased cards have a brighter background, a light edge,
PURCHASED status and an inactive OWNED button. Other cards show prerequisite
or insufficient-credit states. Prices come from BaseUpgrader's existing
serialized price fields; the Drone → Radar → Zeus strike dependency is kept.

BaseUpgrader subscribes to BaseManager.baseUpgrades and team money in
OnNetworkSpawn, refreshes the initial state immediately, and unsubscribes in
OnNetworkDespawn. Resetting the Casino purchase flag also resets its card.
The menu does not introduce separate purchase or save state.

## Held equipment presentation

`Inventory` owns the replicated slot snapshot and selected item. `ItemHolder`
composes camera/shoulder poses and action offsets; `PlayerHeadSync` solves the
existing arm IK; `Item` aligns to the solved palm and manages stored presentation.
WalkieTalkie uses voice transmission to drive a blended raised pose. The system
uses one body and one network item, with a one-handed grip. See `HELD_ITEMS.md`
for prefab authoring, animation and verification.
