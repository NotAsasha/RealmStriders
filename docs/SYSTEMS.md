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

## Casino gate

`Assets/Scripts/CasinoCagePedestal.cs` owns the network interaction on the
CasinoPedistal scene root. `CasinoDoorPresentation` starts an authored Animator
state once, seeking from replicated server time for late joins. It never moves
door transforms itself. `ChipPoint` defines the mounted CasinoCage pose.
All runtime references are serialized. The owner authors the animation and VFX.

On CasinoDoorPresentation, assign `Door Animator`, `Closed State` and
`Opening State` (full paths, default `Base Layer.Closed` / `Base Layer.Opening`).
`Door.controller` has one layer/state machine named `Base Layer` containing
`Closed` and `Opening` states, not separate layers with those state paths.
Closed holds frame zero of the owner's Opening clip at state speed zero;
Opening plays that same clip at speed one (5.033333 seconds). The authored
clip is unchanged. `tools/CasinoAnimatorRepair.cs` Verify checks the actual
prefab/controller, bindings, opening, final hold, reset and late-join pose
in an isolated preview scene.
Use a non-looping opening clip that holds the open pose; do not transition it
back to Closed. Set `Opening Duration` to the actual duration including Animator
state speed. Animate the leaves with their colliders. Animator culling should
be Always Animate so offscreen doors still update collision.
Add lights to `Activation Lights` and particle systems to `Activation Particles`.
Keep their GameObjects active, disable particle Play On Awake, and author colors,
intensities and particle settings in the Inspector. Activation enables lights
and plays particles once; reset disables lights and clears particles. Late joins
enable lights and looping particles without replaying bursts or opening audio.
All presentation references are optional until configured. `tools/CasinoGateSetup.cs`
disables the previous generated light/ring objects and validates pedestal wiring.

Verification through Unity MCP `run_script`: `tools/CasinoGateChecks.cs`
Prepare in Edit Mode, enter Play Mode, Run, stop Play Mode, Restore. The
isolated host/client fixture tests rejection of empty/wrong items, distance
and sender ownership, remote placement, duplicate requests, inventory
removal, mounted presentation, a temporary test Animator, light/particle activation,
collision passage and late joining. The fixture animation does not author the
actual Casino door animation. It does
not load game saves. Existing `SubwooferShaderWarmup` can log an unrelated
shader-pass exception when entering Play Mode.

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
The Shop follows a classic Windows 95/98 reference: cool gray window surfaces,
gray title bars, a teal screen border, raised buttons and inset white fields.
Text uses the existing Liberation Sans SDF font for smooth, readable letters
on angled world-space screens. `tools/ClassicShopStyle.cs` applies this
presentation after the hardware setup and renders an isolated Editor preview.
Item-card Back/Buy captions are assigned from the button identity, so applying
the style repeatedly preserves the Back caption and callback.
Transparent world-space UI hit areas align with the model's five existing
hardware keys. ShopCategoryButton depresses the original mesh and uses a
material property block for selection lighting. Locked keys and status plates
are dark gray with light text. OPEN, READY and LOCKED/STAR labels
distinguish selection from availability. Team balance occupies the
wide hardware display to their right; the catalog header shows team rating.
ShopPanelMotion lowers the physical MainPanel and its canvas, changes the page,
then raises them together using unscaled time. Catalog input is blocked during
travel; hardware keys remain responsive and replace pending navigation.
The same transition handles item details and Back. All hardware and canvas
references are serialized, and Shop binds rating updates during its network
lifetime. The moving terminal is excluded from static batching.
`tools/AnalogShopChecks.cs` exercises the actual Lobby instance in an isolated
loopback fixture, including pointer hit alignment and purchases.
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

## Painterly camera rendering

`Assets/Shaders/PainterlyFullscreen.shader` and
`Assets/Materials/PainterlyFullscreen.mat` provide local, full-screen painterly
stylization. Each of the three quality renderers has a `Painterly Fullscreen`
Full Screen Pass feature, with Fetch Color Buffer enabled and injection after
post-processing. It uses the existing URP feature and works in Scene View too.
The original PainterlyLit object shaders and materials remain independent.

The filter combines four low-variance color regions into brush-like patches,
with luminance quantization and static screen-space texture. It uses a fixed
25-sample neighborhood plus the original pixel; larger strokes do not increase
the sample count. This adds a full-resolution color copy and filter pass;
GPU cost still needs profiling on target hardware, especially at 4K.

Tune the shared material in the Inspector: `Intensity` blends the effect,
`Stroke Radius` is measured in pixels at 1080p, `Stroke Stretch` elongates the
brush, and `Tone Simplification`, `Brush Color Variation`, and `Canvas Texture`
control the finish. Intensity zero preserves the source; disable the renderer
feature to remove its rendering cost. The effect processes the camera image,
including transparent surfaces and fog. Screen Space Overlay UI renders later
and stays unaffected. Camera stacks and render-texture cameras using these
renderers also need a visual check when introducing new camera setups.

Verify in Game View with the feature enabled/disabled, including dark rooms,
bright lights, transparent objects, and camera motion. Switch all three quality
levels and check that the effect persists. It approximates a painted image;
it cannot reproduce PainterlyLit's per-object lighting/normal changes exactly.

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

BaseUpgrader presents all six existing upgrades in the same classic Windows
style as Shop, with smooth text, inset white cards and raised gray controls.
Purchased cards have a pale teal background, a teal edge,
PURCHASED status and an inactive OWNED button. Other cards show prerequisite
or insufficient-credit states. Prices come from BaseUpgrader's existing
serialized price fields; the Drone → Radar → Zeus strike dependency is kept.

BaseUpgrader subscribes to BaseManager.baseUpgrades and team money in
OnNetworkSpawn, refreshes the initial state immediately, and unsubscribes in
OnNetworkDespawn. Resetting the Casino purchase flag also resets its card.
The menu does not introduce separate purchase or save state.
Available, purchased, disabled and status colors are serialized so network
refreshes preserve the light theme. Blocked status text turns dark red on hover.

WorldChooser uses the same window frame, a selected-realm field and three
destination cards. The WorldCard prefab styles generated cards as well as
the authored preview: selected cards have a pale teal background, a teal stripe
and SELECTED text. Current-realm colors are serialized for readable dark red
and dark green states. `tools/ClassicBaseMenus.cs` styles these two terminals;
`tools/ClassicMenuPreview.cs` renders previews and reports text overflow.

## Held equipment presentation

`Inventory` owns the replicated slot snapshot and selected item. `ItemHolder`
composes camera/shoulder poses and action offsets; `PlayerHeadSync` solves the
existing arm IK; `Item` aligns to the solved palm and manages stored presentation.
WalkieTalkie uses voice transmission to drive a blended raised pose. The system
uses one body and one network item, with a one-handed grip. See `HELD_ITEMS.md`
for prefab authoring, animation and verification.
The owner's grip stays ahead of the camera using each item's authored mesh
bounds and a 0.12 m clearance. Local IK adjusts shoulder reach while preserving
arm lengths and palm contact, including the first equip frame. Remote arm reach
and terminal interaction poses retain their existing behaviour.

## Crouching

`PlayerMovement` owns the local crouch request and replicated stance. Its
CharacterController changes height and center together to preserve foot position.
Standing checks only the capsule volume above the crouch pose, ignores triggers
and the player's own hierarchy, and retries until there is clearance. Pause and
terminal interactions preserve stance. Freeze blocks stance input; death clears
it, and teleport resets it where there is room to stand.

The Player prefab uses 2.5 m/s crouch walking and 3.25 m/s crouch running. Noise
radii are 35% and 100% of `walkNoiseRadius`, respectively. Crouch height is 1.6 m
to cover the supplied walking clip, whose posture is higher than the idle clip.
CameraMovement blends a separate 0.65 m camera drop and 0.4 m forward offset so
the viewpoint stays ahead of the crouching torso, remains stable through the
animation cycle and returns correctly after terminal interaction.
All these values are serialized for tuning.

The owner camera's explicit `firstPersonHiddenRenderers` list contains only the
helmet/head mesh. It casts shadows but is hidden in first person, preserving the
same body's visible hands, legs and equipment. Its original rendering mode is
restored after the camera reaches a terminal, on spectator entry and on despawn;
returning to gameplay hides it again. Remote copies retain the complete model.
`tools/CrouchCameraChecks.cs` configures the reference and renders comparisons
using crouch idle/walk poses with forward/up/down look, including spine pitch.

`SettingsSave.crouchMode` persists Hold/Toggle and defaults to Hold for older
settings files. The shared Settings prefab exposes Crouch binding and Crouch Mode
under Controls, using the existing Apply/discard behavior. Runtime movement caches
the mode when settings are applied, so unapplied menu edits do not affect play.

`PlayerAnimation` supplies Crouched/Moving and per-state speed parameters to
SpacemanBody. Animator's global speed stays at one (zero when frozen), allowing
transitions from stationary standing. The two crouch FBXs use Humanoid retargeting,
looping clips and baked root motion; CharacterController owns translation.

Checks through Unity MCP `run_script`:
- `tools/CrouchChecks.cs`: Prepare in Edit Mode, enter Play Mode, Run, stop Play
  Mode, then Restore. The isolated client-owned fixture checks hold/toggle,
  capsule clearance, speeds/noise, jump blocking, pause, lifecycle resets,
  server replication and late joining without touching saves or gameplay scenes.
- `tools/CrouchPoseChecks.cs`: Run in Edit Mode checks the actual character's
  Animator transitions, the settings callback and binding serialization. It
  writes pose previews. CrouchChecks.Preview renders the Controls page.

## Crafter

`Assets/Base/Crafter/Crafter.cs` is a server-authoritative two-input crafting
station. Add recipes to the `Recipes` array in the Crafter Inspector; each
recipe references two input `Item` prefabs and one output `Item` prefab. Input
order is not significant, so separate prefabs can represent upgraded versions
of an existing item without adding special-case stat logic.

`LeftBox` and `RightBox` use `CrafterInputSocket` trigger colliders. The
Crafter validates both dropped network items on the server, despawns the inputs,
waits for `Craft Duration`, and spawns the output at `Output Spawn Point`.
While a slot is occupied, the server periodically validates the cached socket
references, clearing a slot when its item is picked up, parented to an
inventory, or despawned. The validation stops when both slots are empty, so it
does not add per-frame work while the Crafter is idle.
Indicator renderers are red when their socket is empty or not used by any
recipe, and green for a valid ingredient. Door transforms, the shared `Server`
noise AudioSource, and the Tube output point are serialized references on the
Crafter instance.
The Crafter's world-space Canvas displays the server-selected recipe preview.
Each recipe has an Inspector-configured `Craft Text` field that is displayed
on the Canvas, allowing a custom description instead of the prefab names.
Empty slots show `INSERT TWO ITEMS`, while two occupied slots without a
matching recipe show `NO RECIPE` and `ERROR`. If `Craft Text` is empty, the
legacy `Input + Input = Output` preview is used.
