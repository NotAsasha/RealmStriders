# Enemies

## Shared model

Enemy content lives under `Assets/Enemy/`. Shared components provide common
enemy behaviour, movement, sensing, sounds, animation, and spawning. Each
specialized monster extends that foundation with its own rules.

Current specialized enemy areas include:

- `Casino`
- `Grabar`
- `InvisiMan`
- `LureMan`
- `Runner`
- `Shocker`

### Sound conventions

- `EnemySounds` handles vocal presentation (idle breathing, chase roars, jumpscares, death).
- Idle sounds play periodically with randomized intervals while roaming (`minIdleInterval` to `maxIdleInterval`).
- Angry sounds trigger on detection and repeat periodically during chase (`minAngryInterval` to `maxAngryInterval`).
- Vocal audio uses a dedicated `AudioSource` to avoid conflicting with movement footstep attenuation or pitch modulation.
- Vocals are guarded: suppressed while dead, asleep (e.g. music box, static sleep state), frozen, or during capture.
- Attack/jumpscare sounds trigger automatically on player bite via `Enemy.onBite`.

## Spawn model

The enemy spawner runs on the server. Mission selection can provide a
pre-rolled list of danger levels. Danger tiers are gated by the team rating / star level:
- Star 1 (Rating 1–3): Danger 1–2 only (Grabar, Runner). Higher tiers (3–5) are strictly prohibited.
- Star 2 (Rating 4–6): up to Danger 3 (LureMan, Shocker).
- Star 3 (Rating 7–9): up to Danger 4 (CasinoMonster).
- Star 4–5 (Rating 10+): up to Danger 5 (InvisiMan).

Within the allowed tiers, danger probability follows a smooth bell curve centered
around the target danger for the current rating.

The spawner selects a prefab from the configured danger pool, chooses a map position,
moves the object to the mission scene, and spawns its `NetworkObject`.

The current placement logic excludes the portal area and low positions.
However, once a mission is active, enemies are intentionally allowed to
enter the base; the base is not a guaranteed safe zone.

## Monster contract

Before adding or changing a monster, document:

- its role and intended player experience;
- how it detects and tracks players;
- movement and attack states;
- equipment interactions and counterplay;
- capture/defeat conditions;
- server authority and replicated state;
- required prefab, animation, sound, and test-scene changes.

## Capture model

`LeafBlower` is the canonical capture tool. Capture time increases with the
monster's danger or difficulty. Other equipment can weaken a monster before
and during capture; multiple weaknesses can be combined and their effects
stack, reducing the time required by the `LeafBlower`.

The `LeafBlower` keeps the current target and capture progress when the target
briefly leaves its view. While the target is lost, capture does not advance.
After one second without reacquiring that same target, 20% of the required
capture time is removed from progress; this penalty repeats once per second
until the target is reacquired or the progress reaches zero.

Equipment noise is server-authoritative. A running `LeafBlower` or grounded
`Subwoofer` continuously lures nearby enemies, while a walkie-talkie only
lures them while voice data is actively transmitted. `Flashbang` and `Taser`
create one-shot noise when they activate.

`Runner` treats every lure as a threat: on the server it selects the farthest
reachable NavMesh point in the direction away from the sound and runs there.
Repeated continuous noise can update its escape destination. If no complete
escape path is available, it falls back to its normal random running behaviour
instead of becoming stationary.

A successful capture produces an `EnemyCage`. All target monsters must be
captured for a successful mission; killing is not an alternative objective.
The mission may still be ended early from the base, but it gives no rating
increase unless every target was captured.

Items and cages left in the mission zone are destroyed when the mission ends.
A cage already brought into the base remains safe.

## Shocker

The Shocker implementation and its related prefab, energy field, and sound
components are the source of truth for current behaviour. Its design
description, counterplay, and balance targets still need to be confirmed and
should not be invented by an agent.

## Validation checklist

- Test spawning with a host and at least one client.
- Verify that clients cannot create or resolve an enemy encounter locally.
- Check scene ownership and despawn when the mission ends.
- Test the monster against relevant equipment, movement, and player death
  paths.
- Update this document when the intended behaviour changes.
