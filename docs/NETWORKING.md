# Networking

Netcode for GameObjects (NGO) runs gameplay over Facepunch Steam transport.
The server owns shared outcomes. Resolve prices, rewards, damage, capture and
inventory changes from server state. Use `NetworkVariable<T>`,
`NetworkList<T>` and spawned NetworkObjects for state that late joiners need.
Use RPCs for requests and transient events. See
[development conventions](CONVENTIONS.md#verification) for lifecycle rules.

## Session and persistence

Online lobbies are public or friends-only. The Menu Join page refreshes
joinable public lobbies with room. Friends-only games can be joined through a
Steam invitation accepted from Menu. The lobby and NGO connection approval
cap the session at four including the host. New connections are rejected
during a mission and cleanup, including the additive scene transition.
Play Solo uses a single-player loopback host.

The campaign save belongs to the player host. Losing the host sends clients
through SteamBoot to Menu with the message `host left the game`. Host migration
is unsupported. See [persistence](SYSTEMS.md#persistence-and-settings) for
slot handling and [mission lifecycle](SYSTEMS.md#scene-and-mission-lifecycle)
for closure and cleanup.

Peers derive mission time from the replicated server deadline. GameManager
evaluates living player objects after deaths and disconnects and excludes a
departing client from the count. Keep the spectator camera parented to the
player while following a target so it survives dimension unload and returns
to first person on revival. During mission item promotion, preserve the slot
ID and inventory parenting across the unload.

## Inventory and equipment

The server writes slot IDs to `NetworkList<ulong>` (`ulong.MaxValue` means
empty) and owns slot selection, holding state and network parent changes.
Pickup, selection and drop validate the requesting player. Pickup also
checks range, slot occupancy and the existing holder. Directional drop
requests include the owner's origin/forward. The server bounds the origin and
checks for finite values. See [held item lifecycle](HELD_ITEMS.md#lifecycle)
for slot resolution, equip presentation and cleanup.

Held-tool actions validate the active living holder and, for aimed tools,
the supplied aim. The server applies effects and weaknesses and replicates
the resulting Entity state. Equipment-specific contracts include the following.

- Flashbang checks charge and cooldown. Replicated charge and a transient
  pulse represent accepted use.
- Teleporter and ReturnTeleporter each spend 50 of 100 charge on successful
  use, retain the item and enforce a 0.75-second cooldown. Teleporter requires
  an active mission and chooses a safe NavMesh destination outside the base.
  ReturnTeleporter goes to `GameManager.spawnPoint`. Their presentation is
  transient.
- WalkieTalkie refreshes server-written transmission state on validated voice
  packets. It clears after the packet gap, unequip or power-off. The owner
  raises the radio locally while waiting for confirmation and late joiners
  receive current transmission state.
- Packed furniture displays its meshes locally for the active holder. The
  server checks the holder and placement ray, spawns registered furniture
  and consumes the cube.

## Voice, footsteps and gameplay noise

Movement, crouch stance and `currentNoiseRadius` are owner-authoritative.
Peers apply the initial crouch capsule on spawn. Server validation of
displacement, grounded state and head clearance is absent.

`PlayerAudio` predicts the owner's footsteps and requests remote playback
through unreliable owner-only RPCs. The server checks living, unfrozen and
uncaptured state, a valid clip, finite pitch in 0.8–1.2 and a minimum interval
of 0.24 seconds. The owner ignores the accepted echo. Late joiners hear
future steps.

Server-owned equipment state drives continuous gameplay noise. Decoded
microphone RMS above the threshold (default 0.04) refreshes a short-lived
server noise window for AI. Steam Audio spatializes playback independently.
Source binding, additive acoustic volumes and coverage are in
[Steam Audio](STEAM_AUDIO.md).

## Timed presentation and late joins

| Component | Replication and late joins |
| --- | --- |
| `CasinoDoorPresentation` | Seek from the replicated pedestal activation time. Late joins restore the door pose, lights and looping particles. Audio and particle bursts play only on initial activation. State lasts for the mission scene. |
| `CasinoSpinPresentation` | Accepted spins play on connected peers. Late joiners receive future spins. |
| `MissionWarningAudio` | Replicate the active warning window. Play once on entry, including when joining during the window. |
| `NetworkRandomAudio` | Server chooses interval, clip, pitch and future timestamp. Receivers wait or seek on late delivery. Peers share an Inspector clip list and use a dedicated source with Play On Awake disabled. A new event interrupts playback and disable/despawn cancels it. Events are transient and emit no gameplay noise. |
| `MissionLightFlicker` | Server schedules independent wall-lamp bursts from the mission timer. Each event carries a timestamp, duration and seed, allowing receivers to seek on late delivery. Late joiners see future bursts. See [Casino lighting](CASINO_ART.md#lighting-and-navigation). |

Verify sender and ownership checks, initial state, late joins, despawn and
scene transitions with a host and client. Real Steam sessions and microphone
capture require separate verification from UDP loopback checks.
