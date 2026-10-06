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

## Spawn model

The enemy spawner runs on the server. Mission selection can provide a
pre-rolled list of danger levels. The spawner selects a prefab from the
configured danger pool, chooses a map position, moves the object to the
mission scene, and spawns its `NetworkObject`.

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
