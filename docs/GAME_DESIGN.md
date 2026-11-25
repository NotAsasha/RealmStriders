# Game Design

Game Design records owner-confirmed gameplay intent. The gameplay reference
also lists current controls and content. See [Systems](SYSTEMS.md) for
implementation details and gaps.

## High concept

Realm Striders is a tactical survival horror game for 1–4 players. The team
chooses its equipment at a shared base and enters pocket dimensions to hunt
monsters. Captures fund the next expedition.

## Narrative premise

In the 2050s, humanity developed devices that create personal pocket
dimensions. These dimensions can contain dangerous radiation and aggressive
monsters. Specialized teams enter the dimensions, capture the monsters, and
prevent them from escaping into the surrounding world.

## World model

Each mission takes place in a bounded pocket dimension with its own visual
identity and environmental hazards. The portal has a limited safe operating
time, which can vary by mission. Players can check the remaining time with the
Dosimeter.

## Core loop

1. Prepare the team and equipment at the base.
2. Choose a mission world.
3. Enter the mission scene.
4. Locate and identify monsters.
5. Use equipment and teamwork to capture the monsters.
6. Bring the cages back to the base and end the mission there.
7. Sell the captured `EnemyCage` items, buy equipment, and unlock harder
   content.

## Mission success and failure

A mission may be ended from the base at any time. Success requires capturing
every target and ending the mission with a living player at the base. A kill
does not count as a capture. See the [mission lifecycle](SYSTEMS.md#scene-and-mission-lifecycle).

A player who dies becomes a spectator until the mission ends. The rest of the
team can continue. If every player dies, the mission ends automatically and
the team rating decreases by one.

Each capture produces an `EnemyCage`. If the team does not return to the
base, the cages and the mission result are lost. Ending the mission removes
enemies, players and items left in the mission zone. Items and cages brought
into the base survive cleanup.

Progress saves automatically after a mission completes. Manual saving is
available between missions.

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

This section includes design rules and current content and control details.

- The base contains shared progression and services such as the shop,
  upgrades, power, portals, and mission selection.
- Held equipment uses a full-body character and a single right-hand grip.
  See [Held items](HELD_ITEMS.md) for inventory and pose rules.
- `LeafBlower` is the capture tool. Higher-danger monsters take longer to
  capture. Equipment weaknesses stack and reduce the capture time.
  See [Enemies](ENEMIES.md#capture-and-counterplay) for equipment interactions.
- A successful mission increases the shared team rating by one. The rating
  unlocks shop items and harder enemy rolls.
- Money is earned by selling captured `EnemyCage` items at the base.
- Ordinary mission encounters use a rating-scaled threat budget: the same
  budget can produce fewer strong enemies or a larger mixed group.
  See [Enemies](ENEMIES.md#spawning) for unlock thresholds and budget settings.
- Shop categories correspond to five stars. Category 1 is available from
  rating 0. Categories 2–5 unlock at ratings 4, 7, 10 and 13 respectively,
  at the first segment of each subsequent star. Category 4 stocks the
  Portable Charger for 200 credits and the Sprinkler for 125 credits.
  The Portable Charger holds 200 charge. Category 5 stocks the BaseMover.
- Once the portal opens, enemies may enter the base. Players can lure them there.
- In the Casino scene, interacting with the pedestal while holding CasinoCage
  installs it permanently and opens the gate. The cage leaves the inventory
  and cannot be retrieved or sold. Other items do not activate the pedestal.
  The gate stays open for that mission scene's lifetime.
- Players can crouch using a rebindable key (Left Ctrl by default). Controls
  settings select Hold or Toggle; Hold is the default. Crouch walking is slower
  and quieter than standing walking. Holding Run while crouched moves slightly
  faster and emits the same noise radius as standing walking. Crouched players
  cannot jump or stand up beneath an obstruction.

## Campaign completion

At exactly team rating 15, an unfinished campaign offers only the End mission
in WorldChooser. End contains exactly one Grabar, Runner, LureMan, Shocker,
CasinoMonster and InvisiMan, with random positions. Completing End requires
capturing every target and ending the mission with at least one living player
back at the base. It grants the usual +1 rating and permanently marks that
save as completed.

At ratings below 15 or above 15, ordinary missions are available even if the
campaign has not been completed. Completed campaigns offer ordinary missions
at every rating, including 15. Manually raising the rating does not award
campaign completion. Each save card shows the completion star for its own save.
The base's OverRating display appears after completion and shows total stars
as rating divided by three, to one decimal place (rating 16 displays 5.3 STARS).

During End, the portal permits entry from the base but blocks return travel
for players and monsters until no rolled target remains alive. Capturing or
killing the last living target unlocks return travel; killing still does not
count toward successful mission completion. The upper portal is completely
hidden while return travel is locked and reappears when it unlocks.
ReturnTeleporter bypasses the portal restriction with its ordinary charge and
use rules. The existing mission timer continues through the unlock.

A team wipe in End loses one rating point, like an ordinary mission. An
unfinished campaign falling from 15 to 14 offers ordinary locations again,
allowing the team to earn money and rebuild its rating before retrying End.

## Tutorial

The main menu's Tutorial button opens an isolated singleplayer simulation in
the Tutorial scene. It uses one room whose contents change between five tapes:
capture a one-star mannequin with LeafBlower, sell its EnemyCage, buy Taser and
WaterGun, capture an unweakened three-star mannequin, then capture another
three-star mannequin using WaterGun followed by Taser and LeafBlower.

The difficult-target comparison teaches weakening through equipment combinations.
A physical RatingScreen above the mannequin shows its effective difficulty in
whole stars using the production equipment rules.
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
rating and items stay in the simulation. Mannequins do not attack, and the
training selling booth's kill zone is disabled.
