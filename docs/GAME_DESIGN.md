# Game Design

This is the resolved project context based on the current implementation and
the supplied GDD. The GDD is a source of intended design, but it contains
outdated alternatives. Where the GDD and the decisions below differ, this
document is the current design reference.

## High concept

Realm Striders is a cooperative monster-hunting game. A team prepares on a
shared base, enters a mission world, finds and handles monsters with
specialized equipment, and returns to the base to spend the rewards.

## Narrative premise

**Status: Confirmed design**

In the 2050s, humanity developed devices that create personal pocket
dimensions. These dimensions can contain dangerous radiation and aggressive
monsters. Specialized teams enter the dimensions, capture the monsters, and
prevent them from escaping into the surrounding world.

## World model

**Status: Confirmed design; partially implemented**

Each mission takes place in a bounded pocket dimension with its own visual
identity and environmental hazards. A mission world is temporary: its scene
and everything left inside it are removed when the mission ends. The portal
has a limited safe operating time, which can vary by mission.

## Core loop

**Status: Confirmed design; partially implemented**

1. Prepare the team and equipment on the base.
2. Choose a mission world.
3. Enter the mission scene.
4. Locate and identify monsters.
5. Use equipment and teamwork to capture the monsters.
6. End the mission from the base, then return to the base state with any cages
   that were brought there.
7. Sell the captured `EnemyCage` items, buy equipment, and unlock harder
   content.

## Mission success and failure

**Status: Confirmed design; implementation must be verified**

A mission may be ended from the base at any time. It is successful only when
all target monsters have been captured. Killing monsters is not a gameplay
objective or an alternative completion outcome.

A player who dies becomes a spectator until the mission ends; the rest of the
team can continue the mission. If every player dies, the mission ends
automatically and the team rating decreases by one.

Captured enemies produce `EnemyCage` items. If the team does not return to the
base, the cages and the mission result are lost.

Progress is saved automatically after a mission completes and may also be
saved manually while no mission is active. Manual saving during an active
mission is not allowed.

Everything in the mission zone is transient. When the mission ends, enemies,
players, and items left in the mission zone are destroyed. Items and cages
that were brought into the base remain safe.

## Design pillars

- **Cooperation:** equipment and player roles should create reasons to
  communicate and coordinate.
- **Preparation:** the loadout chosen at the base should matter during a
  mission.
- **Tension:** discovering a monster and deciding whether to engage should be
  risky; opening the portal also makes the base vulnerable.
- **Readable counterplay:** every monster should expose clues and have
  understandable ways to respond to it.

## Current gameplay concepts

**Status: Mixed**

The entries below combine confirmed design rules with implementation facts.
Confirmed rules describe intended behaviour; code references describe what is
implemented today and may still need work.

- The base contains shared progression and services such as the shop,
  upgrades, power, portals, and mission selection.
- Missions are loaded as scenes and can spawn a pre-rolled list of enemy
  danger levels. Mission duration is limited and is exposed by the
  `Dosimeter`.
- Players carry equipment from the `Assets/Player/Equipment/` area.
- The current inventory implementation has four configurable slots. The
  supplied GDD's two-hand model and fixed key bindings are not current
  requirements.
- Enemy behaviour is specialized per monster and built on shared enemy
  functionality.
- `LeafBlower` is the capture tool. Higher-danger monsters take longer to
  capture. Other equipment weakens monsters and can be combined; weaknesses
  stack and reduce the time needed by the `LeafBlower`.
- There is one shared team rating, not an individual player rating. A
  successful mission with every target captured gives a fixed increase of one.
  A mission ending after the whole team dies decreases it by one. The rating
  unlocks shop items and influences harder enemy rolls.
- Money is earned by selling captured `EnemyCage` items at the base.
- After the portal opens, enemies may enter the base. The base is not
  guaranteed to be safe, and players may intentionally lure enemies there.
- The game supports networked play through Netcode for GameObjects and a
  Facepunch Steam transport.

## Design decision rule

When gameplay intent is unclear, preserve existing behaviour and record the
open question in documentation or a task. Do not silently turn an
implementation detail into a design requirement.
