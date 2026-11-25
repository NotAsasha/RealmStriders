# Realm Striders

Realm Striders is a cooperative tactical survival horror game for 1–4 players.
Choose a mission at the base, enter a pocket dimension, weaken its monsters
and capture them with the LeafBlower. Bring the cages home to sell and equip
the team for the next hunt.

## Running the game

Open the project in Unity **6000.3.13f1** (Unity 6.3). Enter Play Mode from
[SteamBoot](Assets/Scenes/SteamBoot/SteamBoot.unity) to initialize the game
services and reach the main menu. Select **Host** and a save slot to create
a Steam lobby. When Steam is offline, the same screen offers **Play Solo**.
**Tutorial** starts a separate training session.

## Documentation

| Guide | Use it for |
| --- | --- |
| [Game design](docs/GAME_DESIGN.md) | Mission outcomes, team progression, End and tutorial rules |
| [Systems](docs/SYSTEMS.md) | Finding the code that owns a feature, scene flow, base services and saves |
| [Networking](docs/NETWORKING.md) | Authority, replicated state, RPCs and session lifecycle |
| [Enemies](docs/ENEMIES.md) | Encounter budgets, capture and monster behaviour |
| [Content pipeline](docs/CONTENT_PIPELINE.md) | Adding monsters, equipment, recipes and worlds |
| [Held items](docs/HELD_ITEMS.md) | Authoring grips, poses, IK and inventory presentation |
| [Steam Audio](docs/STEAM_AUDIO.md) | Acoustic sources, geometry, tuning and rebaking |
| [Casino art](docs/CASINO_ART.md) | Art direction, lighting and scene authoring |
| [Development conventions](docs/CONVENTIONS.md) | Implementing, verifying and documenting changes |
| [Agent instructions](AGENTS.md) | Source priority, Unity Editor access and delegation |

Project planning is tracked on the [Realm Striders Trello board](https://trello.com/b/NutR2f53/reals-striders).
