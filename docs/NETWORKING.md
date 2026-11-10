# Networking

Netcode for GameObjects replicates gameplay over Facepunch Steam transport.
Network-aware gameplay components inherit from `NetworkBehaviour`.

## Authority

The server owns shared outcomes. Clients submit requests; the server validates
and applies them. Never trust client prices, rewards, damage, capture results
or inventory changes. Subscribe/apply initial state in `OnNetworkSpawn` and
unsubscribe/clean up in `OnNetworkDespawn`.

Use `NetworkVariable<T>` or networked objects for durable state needed by late
joiners. Use RPCs for requests and transient events. Local presentation must
not be used as proof that an authoritative action succeeded.

| System | Authority and replication |
| --- | --- |
| Mission/spawning | Server generates budget compositions, replicates the selected danger list and loads/unloads NGO scenes; End uses the configured unique prefab roster for both its preview and actual spawn |
| Campaign completion | Server accepts a successful End return, persists the active host slot and replicates completion; late joiners receive the saved state |
| End portal return | Server replicates the transient return lock from mission start and registered target death/capture; player owners and server enemy movement apply it only on the configured return leg |
| Purchases | Server resolves the configured catalog entry, price and rating requirement; client price is ignored |
| Inventory | Server-written slot-ID `NetworkList<ulong>`, selected slot and holding flag; `ulong.MaxValue` means empty |
| Movement/crouch | Existing owner authority; owner-written stance and `currentNoiseRadius` |
| Capture/status | Server resolves capture, damage, Freeze and weaknesses; Entity state is replicated |
| Crafter | Server selects recipe, consumes inputs and spawns output; slot validity/occupancy, recipe index and crafting state are replicated |
| Casino gate | Server validates/consumes CasinoCage once; activation timestamp supports late-join animation seeking |
| Tutorial | Local singleplayer host; server-owned stages and training transactions; no campaign save or Steam lobby |

## Session and persistence

The player host owns the campaign save. A lost host returns clients through
SteamBoot to Menu with `host left the game`; there is no host migration.
Automatic saving follows mission completion. Manual saving is allowed only
outside active missions. Mission completion/rating and disconnect gaps are
listed in [Systems](SYSTEMS.md#known-implementation-gaps); do not infer intended
rules from the legacy loss-threshold branch.

Before unloading a mission, scene items brought into the base are promoted to
runtime NetworkObjects so NGO does not remove them with their original scene.
Inventory parenting and slot IDs must survive this promotion.

## Inventory and equipment

Pickup, selection and drop requests validate the requesting player. Pickup also
checks range, slot occupancy and existing holder. Every peer resolves the slot
snapshot and retries when spawn/parent messages arrive out of order. Network
parent changes belong to the server. Stored roots remain active; explicit
presentation arrays hide visuals/audio and disable held physics/colliders.
Stored items follow the holder for mission-boundary cleanup.

`AddItem` reports request submission, not acceptance. Presentation follows the
replicated result. Despawn removes the slot. Normal drops preserve powered
equipment; selection changes and death turn it off. Held poses and IK are local
presentation; see [Held items](HELD_ITEMS.md). Directional drop requests include
the owner's origin/forward; the server checks finite values and bounds the
origin. Cosmetic poses do not add an aiming protocol.

- Flashbang checks holder, active item, charge and cooldown on the server;
  replicated charge and a transient pulse represent the accepted use.
- Teleporter and ReturnTeleporter both implement `IChargeable`, retain the item
  and spend 50 of 100 charge per successful use, with a 0.75-second cooldown.
  Both validate the active holder and living Human. Teleporter requires an
  active mission and chooses a safe NavMesh destination outside the base;
  ReturnTeleporter returns to `GameManager.spawnPoint` without that mission
  requirement. Audio/VFX/button animation are transient presentation.
- WalkieTalkie's server-written transmission state is refreshed by validated
  voice packets and cleared after a packet gap. The owner starts its cosmetic
  raised pose immediately; late joiners receive ongoing transmission state.
  Unequip/power-off ends it.
- Charger detects dropped items through physics and active held items through
  network-lifetime inventory registration. Both paths use the same range,
  layer, occlusion and nearest-target checks.

## Voice, footsteps and gameplay noise

Movement remains owner-authoritative; crouch replication applies the initial
capsule on every peer, including late joiners. Current movement does not add
server validation of displacement, grounded state or head clearance.

`PlayerAudio` predicts the owner's footsteps and submits unreliable owner-only
RPCs carrying clip index and pitch. The server checks living/unfrozen/uncaptured
state, valid clip, finite pitch in 0.8–1.2 and a 0.24-second minimum interval.
It broadcasts accepted events; the owner ignores the echo and late joiners hear
future steps only. Cadence comes from PlayerMovement.

AI hearing and acoustic playback are separate. Continuous equipment noise is
driven by server-owned state. Voice refreshes a short-lived server noise window
only above the decoded microphone RMS threshold (default 0.04), avoiding static
triggering enemy reactions. Steam Audio results do not change these rules.

`PlayerAudio` binds four independent sources (steps, voice, death, shock).
Owner feedback is 2D; remote sources use Steam Audio. Persistent players resolve
registered acoustic volumes by position when entering additive scenes.
`EnemyAcoustics` binds explicit sources to its scene's environment. Despawn clears
bindings; `VoiceChat` also stops/releases its dedicated streaming clip/buffers.
Authoring and scene coverage are documented in [Steam Audio](STEAM_AUDIO.md).

## Timed presentation and late joins

`CasinoDoorPresentation` seeks the authored opening clip from the replicated
activation time. Late joins enable lights/looping particles without replaying
opening audio or bursts. The mounted cage has no Item/NetworkObject and is not
saveable loot; state lasts for that mission scene only.

`NetworkRandomAudio` selects interval, clip and pitch on the server and sends a
future server timestamp. Clients wait or seek forward after late delivery.
All peers need the same Inspector clip list and a dedicated AudioSource with
Play On Awake disabled. Disable/despawn cancels playback; a new event interrupts
the previous clip. It emits no gameplay noise and stores no event history.

For any new interaction, check sender/ownership, authoritative validation,
initial state, late joining, despawn and scene transitions with a host and a
client. UDP loopback checks do not verify Steam transport or microphone capture.
