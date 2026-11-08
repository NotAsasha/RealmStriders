# Game Design

This is the current design reference, after explicit owner decisions.
Implementation details are documented in [Systems](SYSTEMS.md); code that
disagrees with these confirmed rules is an implementation gap.

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

**Status: Confirmed design; known implementation gaps in [Systems](SYSTEMS.md#known-implementation-gaps)**

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
- `Flashbang` is rechargeable like `Taser`: each activation consumes 50 of
  its 100 charge, and the item remains when empty. Its indicator is green at
  100%, yellow at 50–99%, and red below 50%. Each activation repels enemies
  within 30 m and displays a brief expanding pulse matching that radius.
- The current inventory implementation has four configurable slots.
- Held equipment uses the existing full-body character and a single right-hand
  grip. Separate first-person arms and two-handed poses are outside the current
  scope. A powered, equipped radio is raised while transmitting voice, then
  lowered after transmission ends.
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
- Shop categories correspond to five stars. Category 1 is available from
  rating 0; categories 2–5 unlock at ratings 4, 7, 10 and 13, respectively
  (the first segment of each subsequent star). Existing item grouping and
  prices are unchanged; the last two categories currently have no stock.
- After the portal opens, enemies may enter the base. The base is not
  guaranteed to be safe, and players may intentionally lure enemies there.
- The game supports networked play through Netcode for GameObjects and a
  Facepunch Steam transport.
- In the Casino scene, interacting with the pedestal while holding CasinoCage
  installs it permanently and opens the gate. The cage leaves the inventory
  and cannot be retrieved or sold. Other items do not activate the pedestal.
  The gate stays open for that mission scene's lifetime.
- Players can crouch using a rebindable key (Left Ctrl by default). Controls
  settings select Hold or Toggle; Hold is the default. Crouch walking is slower
  and quieter than standing walking. Holding Run while crouched moves slightly
  faster and emits the same noise radius as standing walking. Crouched players
  cannot jump or stand up beneath an obstruction.

## Tutorial

**Status: Confirmed design; implemented**

The main menu's Tutorial button opens an isolated singleplayer simulation in
the Tutorial scene. It uses one room whose contents change between five tapes:
capture a one-star mannequin with LeafBlower, sell its EnemyCage, buy Taser and
WaterGun, capture an unweakened three-star mannequin, then capture another
three-star mannequin using WaterGun followed by Taser and LeafBlower.

The difficult-target comparison teaches weakening through equipment combinations,
not crafting or upgrading LeafBlower. A physical RatingScreen above the mannequin
shows its effective difficulty in whole stars using the production equipment rules.
Taser on a wet target removes one effective star temporarily; the base danger
rating does not change. The final tape requires both the combo and a capture.

There is no additional tutorial HUD except the cassette-change transition.
Short physical signs identify the tools to buy/combine; the difficult capture
tape has a sign reading `Drop Item: <configured key>`, refreshed after rebinding.
Each tape waits 3.3 seconds after completion, then removes previous equipment
and props beneath the cassette/grain overlay. The first captured cage is retained
for the selling tape. F5 repeats the current tape with fresh equipment.
The last tape returns automatically to Menu with `tutorial completed`, using the
same notification presentation as `host left the game`. Training credits,
rating and items never enter campaign saves.
Mannequins do not attack, and the training selling booth's kill zone is disabled.

## Design decision rule

When gameplay intent is unclear, preserve existing behaviour and record the
open question in documentation or a task. Do not silently turn an
implementation detail into a design requirement.
