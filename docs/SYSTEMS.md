# Systems Overview

This is a map of the current implementation. Confirmed gameplay rules live in
[Game Design](GAME_DESIGN.md); known differences are listed below.

## System map

| Area | Entry points | Responsibility |
| --- | --- | --- |
| Game flow | `Assets/GameManager.cs` | Mission scene, timer, cleanup, team money and rating |
| Player | `Assets/Player/` | Human/Entity, movement, inventory, equipment, UI and voice |
| Enemies | `Assets/Enemy/` | Server spawning, detection and monster behaviour; see [Enemies](ENEMIES.md) |
| Base | `Assets/Base/` | Shop, selling, upgrades, power, portals, radar and mission selection |
| Persistence | `Assets/FileSystem/Scripts/` | Save files, saveable item registry and local settings |
| Steam/bootstrap | `Assets/Steam/`, `Assets/Plugins/Facepunch/` | Startup, lobbies, transport and disconnect handling |
| Tutorial | `Assets/Scripts/Tutorial/` | Isolated singleplayer training using production equipment |
| Audio | `PlayerAudio`, `EnemyAcoustics`, `ItemAcoustics`, `SceneAcoustics`, `SpatialLoopEmitter` | Local playback and acoustic binding; see [Steam Audio](STEAM_AUDIO.md) |
| Rendering | `Assets/Shaders/`, `Assets/Materials/`, `Assets/Settings/` | URP presentation and painterly fullscreen effect |

## Scene and mission lifecycle

Enabled build scenes are SteamBoot, Menu, Lobby, Field, Casino, Library and
Tutorial. World1 and World2 remain disabled legacy entries, not current mission
setup references.

SteamBoot initializes services. `LobbyUiManager` handles Menu pages and requires
an explicit save selection before hosting. It assigns `GameManager.currentSave`
and continues only after NGO starts the host. Per-slot deletion requires
confirmation, clears that slot's selection and preserves other saves/settings.
The Join page is a placeholder; `SteamManager` already has lobby discovery,
join-by-ID and Steam invitation paths, but they are not a completed menu browser.

The server loads mission scenes additively through NGO, selects the loaded
mission as active for NavMesh queries, then spawns the pre-rolled enemy list.
Mission closure can be requested from the base, triggered by the timer or by
the living-player count reaching zero. Cleanup removes unreturned items,
unloads the mission, revives players and saves the host's progression.

Base membership during cleanup is a distance check against `spawnPoint` and
`baseRadius`. Scene items brought into that radius are promoted to runtime
NetworkObjects before unloading the mission; held items retain their inventory
identity. `ItemReturnZone` separately returns items that fall below the lobby
to its configured shop-tube spawn point.

On timer expiry, `Human.KillForMissionEnd` sends an owner-only `PORTAL CLOSED`
notice to living players left outside the base. `PortalClosedNotice` survives
the immediate revival, uses unscaled time and clears on despawn. Manual closure
and earlier deaths do not show it.

## Player and inventory

`Human` extends shared `Entity` health/status/capture behaviour. Movement is
owner-authoritative. `PlayerMovement` replicates crouch stance and updates the
capsule while preserving foot position; standing waits for head clearance.
Freeze blocks stance input, death clears it, and teleport resets it where
standing is possible. `CameraMovement` blends the crouch viewpoint and hides
only its explicitly assigned first-person head/helmet renderers.

Inventory slots and selection are server-owned. `ItemHolder` composes held
poses, `PlayerHeadSync` solves the existing full-body arm IK, and `Item` aligns
to the solved palm. See [Held items](HELD_ITEMS.md) for authoring and lifecycle
details and [Networking](NETWORKING.md) for the trust boundary.

## Base services

- Shop prices and rating requirements come from configured `ItemCard` entries
  on the server. `CategorySwitch`, `ShopCategoryButton` and `ShopPanelMotion`
  manage navigation and the physical terminal. `ShopItemPreview` renders
  mesh-only previews in a separate local scene and stops its camera when closed.
- `BaseManager` owns shared upgrades; `BaseUpgrader` presents their state and
  affordability without a second purchase/save state. `WorldChooser` selects
  missions and their danger rolls.
- `PowerGrid` owns base charge; charge and upgrades are included in the save.
  `Charger` selects eligible dropped items or the active equipped item on the
  server, checking range, layer and occlusion. Stored slots are ineligible.
- `Crafter` is the current two-input station. The server matches unordered
  recipes, consumes inputs, waits for the configured duration and spawns output.
  Occupied sockets are revalidated until empty. Replicated slot/recipe state
  drives indicators and preview text. Recipe setup is in [Content Pipeline](CONTENT_PIPELINE.md).

## Persistence and settings

The host owns campaign saves. `SaveFile` stores money, team rating, base upgrades,
power charge and registered saveable objects. `NetworkItemsHandler` restores
objects through the `NetworkSaveables` registry. Preserve existing IDs when
changing content. Automatic saving follows mission completion; the manual save
button rejects an active mission.

`SettingsFile` owns local input, sensitivity, audio, graphics and crouch mode.
Settings are shared by Menu and Pause: Apply persists pending values, closing
discards them, and Restore Defaults remains pending until Apply. Master volume
previews immediately through `AudioListener.volume`. Hold is the default crouch
mode, including for older settings files. Graphics use Unity quality, display,
VSync, frame-rate and shadow settings; there is no post-processing toggle yet.

## Casino gate

`CasinoCagePedestal` validates and consumes CasinoCage on the server. An activation
timestamp drives the installed mesh and `CasinoDoorPresentation`; late joiners
seek to the corresponding opening pose. The mounted cage is mesh-only scene
content, not a pickup or saveable item.

Assign the Animator, full state paths (`Base Layer.Closed`, `Base Layer.Opening`),
opening duration, ChipPoint, audio, lights and particles explicitly. Closed holds
the opening clip at frame zero; Opening plays once and holds the open pose.
Duration must include state speed. Animate colliders with the leaves and use
Always Animate culling. Keep activation objects active and particle Play On
Awake off. Late joins enable lights/looping particles without replaying bursts
or opening audio. The owner authors the animation and VFX.

## Tutorial

`TutorialSession` loads services if necessary, clears the campaign save selection
and starts a local singleplayer host without a Steam lobby. `TutorialDirector`
owns five tapes and advances from authoritative capture, sale and purchase events.
All spawned training items, including held items and purchases, are tracked for
cleanup. Networked props are explicitly spawned/despawned rather than relying
on an inactive stage parent.

`TutorialDummy` uses production Entity capture/status effects without AI.
The physical difficulty RatingScreen has `followTeamRating` disabled. Training
starts with 500 temporary credits and rating 4; the shop disables emergency
LeafBlower spawning. Only the training Charger bypasses the base-upgrade
requirement, and it is active only for the final tape. The cassette overlay uses
an assigned URP grain Volume and AudioSource, persists through the final
bootstrap transition and then destroys itself. Campaign files are never assigned
or written. Tape behaviour is defined in [Game Design](GAME_DESIGN.md#tutorial).

## Presentation

Menu and Pause use the shared graphite/font style and unscaled CanvasGroup
transitions. Shop, BaseUpgrader and WorldChooser use the classic gray desktop
terminal style. References and colors are authored in the Inspector.

`UIControlSound` supplies local click/submit feedback through persistent 2D
`Resources/UIAudio`. Disabled controls and hover/focus changes stay silent.
`MissionWarningAudio` replicates the final mission warning window; its assigned
source plays once on entry, including for a client joining during that window.
`NetworkRandomAudio` schedules server-selected clip/pitch events against server
time; it has no gameplay-noise role and late joiners hear future events only.

`PainterlyFullscreen.shader` and its shared material apply the URP fullscreen
filter after post-processing. Tune intensity, stroke radius/stretch, tone,
color variation and canvas texture on the material. Overlay UI stays unaffected.
Disabling the renderer feature removes its pass cost; intensity zero only
removes the visible effect. Check every quality tier and profile on target
hardware after changing it.

## Known implementation gaps

- **Mission rating:** confirmed design requires every target captured for +1,
  and a team wipe for −1. `GameManager.CalculateRating` currently checks enemy
  `isDead`: all dead gives +1; none dead gives −1 when rating is positive,
  regardless of the living-player count. Capture also sets `isDead`, so this
  does not distinguish a capture from a kill.
- **Legacy loss threshold:** `lossRating` is still serialized and
  `StopMissionClock` still disconnects when rating is at/below it, before normal
  cleanup/save. This deprecated branch is not a confirmed game-over rule.
- **Disconnect completion:** `SteamManager` publishes remote disconnect events,
  but `GameManager` currently decrements its living-player count through death
  notifications. The case where all living clients leave a dead host needs
  verification; it must not be documented as working automatically.

These are implementation gaps, not changes to the confirmed design. No runtime
fixes are implied by documenting them.
