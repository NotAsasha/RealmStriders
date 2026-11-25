# Enemies

Enemy content lives under `Assets/Enemy/`. `Enemy`, `EntityDetector`, movement,
`EnemySounds` and `EnemySpawner` provide shared behaviour. Current specialized
areas are Casino, Grabar, InvisiMan, LureMan, Runner and Shocker.

## Spawning

The server spawns the mission's pre-rolled danger list. `EnemySpawner` chooses
from Inspector-configured pools, samples a map position, moves the instance to
the mission scene and spawns its NetworkObject.

| Rating used for the mission card | Highest eligible spawn-pool tier |
| --- | --- |
| 1–3 | 2 (Grabar, Runner) |
| 4–6 | 3 (adds LureMan, Shocker) |
| 7–9 | 4 (adds CasinoMonster) |
| 10+ | 5 (adds InvisiMan) |

WorldChooser offsets the team rating by −1, 0 and +1 for its three ordinary
cards, with a minimum card rating of 1. Each card spends a threat budget of
its rating + 2, capped by
`EnemySpawner.maximumThreatBudget` (code default 20) and by what fits in the
mission payload at the unlocked tiers. Each spawn-pool tier costs its index,
from 1 to 5. The generator chooses a feasible
enemy count, then spends the whole budget without exceeding the unlocked tier
or the seven-target payload limit. Results are shuffled. A budget of 20 can
produce four tier-5 targets or a larger mixed group. Weak tiers remain eligible
at high ratings.

Ordinary mission cards average the rolled pool tiers. Capture difficulty comes
from each prefab's `dangerLevel`, which can differ from its pool tier. Grabar is
in pool 1 and has danger level 2. Hit points are a separate stat. The InvisiMan
prefab starts with 5 HP.

End bypasses budget rolls and uses `finalMissionEnemies`, explicitly configured
on the GameManager prefab: Grabar, Runner, LureMan, Shocker, CasinoMonster and
InvisiMan, once each. Its card derives six targets and their difficulty from
that same roster. Positions remain random. Missing/duplicate roster entries or
an oversized roster are configuration errors; there is no random replacement.
All prefab references and spawn positions are prepared before spawning any
target, so a failed placement cannot silently produce a partial final encounter.

Placement excludes the configured portal area and positions below Y = −30.

## Capture and counterplay

See [Game Design](GAME_DESIGN.md#mission-success-and-failure) for success,
rating and cage recovery. GameManager counts capture events from registered
mission targets.

`LeafBlower.CalculateRequiredHoldTime` subtracts active weakness stacks from
the target's rounded danger level. Zero and one effective star both use
`baseCaptureTime`. Each additional star multiplies the hold time by
`starTimeMultiplier`.

- Sleeping or invincible entities cannot accumulate capture progress or enter
  the server capture sequence. Static in-base CasinoMonster and LureMan become
  capturable only after their wake-up sequence clears those effects.
- The holder and their child colliders are excluded from target detection;
  dropping removes that exclusion.
- Losing sight retains the target/progress but pauses capture. After one second
  without reacquiring that target, progress loses 20% of the required capture
  time, repeating each second until reacquired or empty.

Flashbang holds 100 charge and spends 50 per use. It remains in the inventory
when empty and can be recharged. Its indicator is green at 100%, yellow at
50–99% and red below 50%. `effectRadius` defaults to 30 m and controls both the
flee effect and pulse size. `expandDuration` (0.5 s) and `fadeDuration` (0.25 s)
set the pulse timing and together determine the reuse delay. An enemy flees if
it has a valid escape route.

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

`ShockerCageDischarge` selects a uniformly random living, spawned entity.
The code defaults are a 6 m radius and a 4–9 second interval. Players
(including the carrier) and monsters qualify while alive, vulnerable and
outside the capture sequence. Selection uses root distance and ignores line
of sight. The cage stays active when dropped, held or stored and needs no charge.

It applies Freeze and, on a wet target, refreshes weakness slot 3. Code
defaults are 2 seconds of Freeze and 6 seconds of weakness. A discharge leaves
HP unchanged. Set the radius and timings in the Inspector.
The server owns selection/effects, a transient RPC plays lightning/audio, and
Entity replicates Freeze/weakness. Despawn stops timers and presentation.

Shocker's role, counterplay and balance targets remain open. Host/client
playtesting must include a stored cage and mission-end despawn.

## Adding or changing a monster

Follow the [monster authoring checklist](CONTENT_PIPELINE.md#monsters).
Verify server-owned spawning and encounter resolution, counterplay, player
death, capture and mission-end cleanup with a host and client.
