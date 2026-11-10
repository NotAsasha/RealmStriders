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

Ordinary encounters spend a threat budget of rating + 2, capped by the spawner's
`maximumThreatBudget` (20 by default) and the mission payload capacity. Each
spawn-pool tier costs its index, from 1 to 5. The generator chooses a feasible
enemy count, then spends the whole budget without exceeding the unlocked tier
or the seven-target payload limit. Results are shuffled. A budget of 20 can
produce four tier-5 targets or a larger mixed group; weak tiers remain eligible
at high ratings. Three mission cards retain their rating offsets of -1, 0, +1.

Threat cost is separate from capture difficulty: existing pool assignments and
prefab stats are preserved. In particular, Grabar currently occupies pool 1
while its prefab has dangerLevel 2. The ordinary card's danger average still
describes the rolled pool tiers, not each prefab's capture difficulty.

End bypasses budget rolls and uses `finalMissionEnemies`, explicitly configured
on the GameManager prefab: Grabar, Runner, LureMan, Shocker, CasinoMonster and
InvisiMan, once each. Its card derives six targets and their difficulty from
that same roster. Positions remain random. Missing/duplicate roster entries or
an oversized roster are configuration errors; there is no random replacement.
All prefab references and spawn positions are prepared before spawning any
target, so a failed placement cannot silently produce a partial final encounter.

Placement excludes the configured portal area and low positions. This does not
make the base safe: enemies may enter it during an active ordinary mission.

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

Mission success/rating follows [Game Design](GAME_DESIGN.md). GameManager counts
registered targets' capture events; killing a target does not count as capture.

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
