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
| Persistence | `Assets/FileSystem/` | Save files and load/delete operations |
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
- The host owns the save file; automatic saves happen after mission
  completion, while manual saves are allowed only outside active missions.
- Mission-zone objects are transient; objects left outside the base are
  destroyed when the mission ends, while objects brought into the base remain.

When adding a networked feature, identify its authoritative owner before
adding a `NetworkVariable`, RPC, or local cache.

## Scene flow

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
