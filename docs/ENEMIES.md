# Enemies

Enemy content lives under `Assets/Enemy/`. `Enemy`, `EntityDetector`, movement,
`EnemySounds` and `EnemySpawner` provide shared behaviour. Current specialized
areas are Casino, Grabar, InvisiMan, LureMan, Runner and Shocker.

## Spawning

The server spawns the mission's pre-rolled danger list. `EnemySpawner` chooses
from Inspector-configured pools, samples a map position, moves the instance to
the mission scene and spawns its NetworkObject.

| Team rating | Maximum danger |
| --- | --- |
| 0–3 | 2 (Grabar, Runner) |
| 4–6 | 3 (adds LureMan, Shocker) |
| 7–9 | 4 (adds CasinoMonster) |
| 10+ | 5 (adds InvisiMan) |

Within the allowed tiers, weights form a smooth bell curve around the rating's
target danger. Placement excludes the configured portal area and low positions.
This does not make the base safe: enemies may enter it during an active mission.

## Capture and counterplay

Confirmed rules: LeafBlower captures every target; higher danger takes longer.
Other equipment weakens monsters, and stacked weaknesses shorten capture time.
Capture produces an EnemyCage. Killing is not an alternative objective. Cages
must reach the base before mission cleanup to remain available.

Current LeafBlower behaviour:

- The holder and their child colliders are excluded from target detection;
  dropping removes that exclusion.
- Losing sight retains the target/progress but pauses capture. After one second
  without reacquiring that target, progress loses 20% of the required capture
  time, repeating each second until reacquired or empty.

Mission success/rating follows [Game Design](GAME_DESIGN.md). The current
`isDead`-based completion calculation has a documented
[implementation gap](SYSTEMS.md#known-implementation-gaps).

Equipment noise is server-owned: running LeafBlower and grounded Subwoofer
continuously lure, radio lures while voice is transmitted, and Flashbang/Taser
emit activation noise. Audible spatialization does not determine AI hearing.

`Enemy.Run(sourcePosition)` finds a reachable NavMesh destination away from the
source, preferring the farthest valid point and trying shorter distances in
confined spaces. Only complete paths using the agent's navigation type/areas
qualify; failure preserves movement. Fleeing takes priority over pursuit until
the escape path finishes. Flashbang supplies its pulse origin.

Runner treats every lure as a threat and uses that same escape path. Continuous
noise can update the destination. Its parameterless damage response uses a
visible/heard threat or retreats backwards. Tune `fleeDistance`,
`fleeDirectionSamples` and `navMeshSampleDistance` in the Inspector.

## Sounds

`EnemySounds` owns idle, angry, jumpscare and death vocal presentation. Idle
intervals apply while roaming; detection starts angry playback with chase
intervals. Vocals are suppressed when dead, asleep, frozen or capturing.
`Enemy.onBite` triggers the attack/jumpscare cue.

Keep vocals separate from footstep pitch/range changes. Assign vocal sources
explicitly, including separate music/vocals on LureMan. `EnemyAcoustics` binds
configured source pairs on spawn and clears them on despawn. Scene data and
source authoring are in [Steam Audio](STEAM_AUDIO.md).

## Shocker cage

`ShockerCageDischarge` selects a uniformly random living, spawned entity within
6 m every 4–9 seconds. Players (including the carrier) and monsters qualify;
dead, capturing and invincible entities do not. Root distance is used without
line of sight. The cage stays active dropped, held or stored, without charge.

It applies 2 seconds of Freeze and, on a wet target, refreshes weakness slot 3
for 6 seconds; it deals no health damage. Range/timing are Inspector settings.
The server owns selection/effects, a transient RPC plays lightning/audio, and
Entity replicates Freeze/weakness. Despawn stops timers and presentation.

Shocker's wider role, counterplay and balance targets still need owner
confirmation. Do not invent them from its current implementation. Host/client
playtesting must include a stored cage and mission-end despawn.

## Adding or changing a monster

Define role, detection/tracking, movement/attack states, equipment interactions,
capture conditions and failure cases before implementation. Reuse shared enemy
abstractions, document authority/replicated state and explicitly configure the
prefab, animations, audio and spawn pool. See [Content Pipeline](CONTENT_PIPELINE.md).

Verify host/client spawn, counterplay, player death, capture and mission-end
cleanup. Clients must not create or resolve encounters locally.
