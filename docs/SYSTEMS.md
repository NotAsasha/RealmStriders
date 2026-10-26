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

## Held equipment presentation

`Inventory` owns the replicated slot snapshot and selected item. `ItemHolder`
composes camera/shoulder poses and action offsets; `PlayerHeadSync` solves the
existing arm IK; `Item` aligns to the solved palm and manages stored presentation.
WalkieTalkie uses voice transmission to drive a blended raised pose. The system
uses one body and one network item, with a one-handed grip. See `HELD_ITEMS.md`
for prefab authoring, animation and verification.
