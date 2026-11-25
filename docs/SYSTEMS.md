# Systems Overview

For gameplay rules, see [Game Design](GAME_DESIGN.md). For replication and
session admission, see [Networking](NETWORKING.md).

| Area | Entry points |
| --- | --- |
| Mission flow and campaign | `Assets/GameManager.cs`, `CampaignProgression`, `WorldChooser` |
| Players and equipment | `Assets/Player/`, [Held items](HELD_ITEMS.md) |
| Enemies and encounters | `Assets/Enemy/`, [Enemies](ENEMIES.md) |
| Base services | `Assets/Base/` |
| Save files and local settings | `Assets/FileSystem/Scripts/` |
| Steam startup and sessions | `Assets/Steam/`, [Networking](NETWORKING.md#session-and-persistence) |
| Tutorial | `Assets/Scripts/Tutorial/` |
| Audio authoring | [Steam Audio](STEAM_AUDIO.md) |
| Rendering and content authoring | `Assets/Shaders/`, `Assets/Materials/`, `Assets/Settings/`, [Content Pipeline](CONTENT_PIPELINE.md) |

## Scene and mission lifecycle

The build starts in SteamBoot, which initializes services before Menu. The
campaign base loads in Lobby. Field, Casino, Library and End are mission scenes.
Tutorial starts a separate training session. `LobbyUiManager` requires an
available save selection before hosting or choosing Play Solo. An online
failure stays in Menu with feedback and can be retried.

`GameManager` loads the mission additively, makes it active for NavMesh queries
and spawns the pre-rolled roster. WorldChooser uses GameManager's cached
spawner for both ordinary rolls and the End preview. End's explicit prefab
roster keeps the preview and actual encounters aligned. The spawner validates
the full prefab/position plan before instantiating. GameManager tracks
`Entity.CapturedServer` separately from the remaining enemy list. Success
requires the full roster to spawn and be captured and a living player to be
at the base at closure.

A living operator can close the mission using the base lever. The server also
handles timeout, base alarm consequences and team wipes observed on death or
disconnect. Cleanup blocks mission selection, startup and manual saves. The
rating check uses `baseRadius` around `spawnPoint` at closure. An early return
with a survivor and outstanding targets keeps the rating. If nobody is alive
at the base, the team loses one point, down to a minimum of zero. Item cleanup
and save snapshots use position alone. A cage already delivered can therefore
survive even if the last living player later dies outside the base. The
[design rule](GAME_DESIGN.md#mission-success-and-failure) calls for losing
the cages when the team fails to return. Cleanup promotes
returned scene items to runtime NetworkObjects while preserving held inventory
identities, unloads the scene, revives players and saves progression.
`ItemReturnZone` separately returns items that fall below the lobby to the
configured shop-tube spawn.

End sets the server-written `endReturnLocked` during mission startup. It
unlocks once the full roster has spawned and no registered target remains
alive. Capture finalization also sets the target's death state.
`PortalTeleporter.returnsToBase` marks the affected leg for player and enemy
travel. `PortalManager` also gates the assigned upper portal root, including
its renderers and trigger, and applies the initial lock on spawn.
`ReturnTeleporter` is independent of that gate.

On timeout, `Human.KillForMissionEnd` sends the owner-only `PORTAL CLOSED`
notice to living players outside the base. `PortalClosedNotice` uses unscaled
time, survives immediate revival and clears on despawn.

## Player and base services

- `Human` extends `Entity` for health, status and capture. `PlayerMovement`
  preserves foot position when resizing the crouch capsule and checks head
  clearance before standing. Freeze blocks stance input, death clears it and
  teleport resets it where standing is possible. `CameraMovement` blends the
  crouch viewpoint and hides its assigned first-person head and helmet renderers.
- Shop resolves catalog prices and rating requirements server-side. Purchases,
  emergency equipment and in-base casino prizes use the shop tube. Crafter has
  a separate outlet. Both use `ItemDeliveryTube.DispenseServer` for spawning
  and client sound and particles. See [shop previews](CONTENT_PIPELINE.md#shop-previews)
  and [crafting recipes](CONTENT_PIPELINE.md#crafting-recipes) for authoring.
- `BaseManager` owns shared upgrades. `BaseUpgrader` presents their live state
  and affordability. `PowerGrid` owns base charge. `Charger` checks layer,
  range and occlusion for dropped items and the active equipped item on the
  server. Stored slots are ineligible.
- `Crafter` matches unordered inputs on the server, revalidates occupied
  sockets, consumes the inputs and spawns the output after the recipe duration.
  Replicated slots, recipe and crafting state drive the UI.
- Radar's local camera updates only when its assigned screen is visible to the
  player camera. It samples screen center and corners for occlusion at 10 Hz
  using the authored mask. The Inspector frame cap is 1–60 (default 30),
  measured in unscaled time. Skipped updates retain the last render texture.
  Server targeting and strike timers run independently.

## Persistence and settings

The active host slot snapshots live state. `SaveFile` stores campaign completion,
ratings, money, base upgrades, charge and registered saveable objects.
`NetworkItemsHandler` restores objects through stable `NetworkSaveables`
prefab IDs. Preserve those IDs when updating content. Format 2 tracks scene
object identity and removed base furniture so unpacking persists a single
replacement. Empty slots start with the authored base.

`GameFile` validates the snapshot, writes and flushes a temporary file, then
atomically replaces the slot and retains the previous valid file as a backup.
Loading falls back to the backup if the primary is missing or invalid.
An unsupported newer primary
blocks loading. If the base cannot restore the selected slot, the host
returns to Menu with feedback and the files remain available for recovery.
Manual saves and mission autosaves report their result. Confirmed deletion
clears the selected slot, its backup and selection while preserving other
slots and settings.

Older files default `campaignCompleted` to false. `GameManager` replicates the
flag and sets it permanently after a successful End return. `LoadSaveButton`
uses it for the slot star. `CampaignRatingDisplay` observes rating and
completion, updates the assigned OverRating panel, and formats total stars to
one decimal place. See the [End selection rules](GAME_DESIGN.md#campaign-completion).

`SettingsFile` owns local input, sensitivity, audio, graphics and crouch mode.
Menu and Pause share the settings. Apply saves pending values, including Restore
Defaults, and closing discards them. Master volume previews through
`AudioListener.volume`. Hold is the default crouch mode, including for older
settings. Graphics controls cover quality, display, VSync, frame rate and
shadows.

`TerrainGraphicsQuality` scales grass on the assigned Terrain instance in
Field and Menu at enable and when graphics settings apply. Performant uses
30% of authored density and 50% of detail distance, Balanced uses 65% and 75%,
and High Fidelity uses the authored values. The component preserves TerrainData
and restores authored values on disable.

## Casino slot machine and gate

`SlotMachine` checks the spin lock and funds server-side before charging,
selecting the result and broadcasting accepted presentation. `CasinoSpinPresentation`
plays the isolated lever clip after the body Animator so the handle can move
while the monster is dormant. Assign the handle, clip, effects and drum
sources and the handle, `SlotClick` and stop sounds. The two sources bind
through `EnemyAcoustics`. Drum clicks start in quick succession and spread out
as the spin nears completion. Despawn stops the sources and restores the handle.

`CasinoCagePedestal` consumes CasinoCage server-side. Its replicated activation
time drives the mesh-only mounted cage and `CasinoDoorPresentation`. Assign
the Animator, `Base Layer.Closed` and `Base Layer.Opening` state paths, duration,
ChipPoint, audio, lights and particles. Closed holds frame zero and Opening
holds the final pose. Include state speed in the duration, animate the leaf
colliders, use Always Animate culling, leave activation objects active and
disable particle Play On Awake. See [timed presentation](NETWORKING.md#timed-presentation-and-late-joins)
for late-join behavior and [Casino art](CASINO_ART.md) for scene authoring.

## Tutorial

`TutorialSession` initializes services as needed, clears the save selection
and starts a local host. `TutorialDirector` advances five tapes from
authoritative capture, sale and purchase events and tracks all training items
for cleanup, including held items and purchases. Networked props are explicitly
spawned and despawned. `TutorialDummy` uses production capture and status
without AI.

Training starts with 500 temporary credits and rating 4. Disable
`followTeamRating` on the physical difficulty RatingScreen and emergency
LeafBlower spawning in the training shop. The training Charger bypasses
upgrades only here and activates for the final tape. The cassette overlay
uses an assigned grain Volume and AudioSource and survives the final bootstrap
transition before destroying itself. See [Tutorial design](GAME_DESIGN.md#tutorial)
and [ceiling clock authoring](CONTENT_PIPELINE.md#tutorial-ceiling-clock).

## Presentation

- `UIControlSound` plays enabled click/submit feedback through the persistent
  local 2D `Resources/UIAudio` source. Hover and focus changes are silent.
- `PainterlyFullscreen.shader` applies the shared URP filter after
  post-processing. Tune intensity, stroke radius and stretch, tone, color
  variation and canvas texture on the material. Overlay UI is unaffected.
  Disable the renderer feature to remove the render pass.
- `PortalCamera` disables rendering while hidden, outside view/range or
  throttled by the Performant distance check. The behaviour stays active to
  wake its Camera and keeps the previous render texture. `PortalManager`
  creates the textures before first opening.
- Reserve renderer index 1 for `URP-Radar-Renderer` in each quality asset.
  It uses Forward with the RadarOnly marker pass. Main renderers use Auto
  intermediate textures. Fog and distortion passes request intermediates
  only for eligible cameras and active effects. Equipment and scene shaders
  still sample depth/opaque textures.
- `GameplayShaderWarmup` loads and retains `Resources/GameplayShaderVariants`
  before the first scene and skips graphics-less servers. Build-time generation,
  shader settings and Player verification are in the [content pipeline](CONTENT_PIPELINE.md#shader-warmup).
