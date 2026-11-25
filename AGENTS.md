# Working in Realm Striders

Realm Striders is a Unity 6 URP project using Netcode for GameObjects (NGO)
and Facepunch Steamworks. Start in the feature's directory under `Assets/`.
Read its local `AGENTS.md` and the relevant [project guide](README.md#documentation).

## Design decisions

When sources disagree, use this order:

1. Explicit decisions confirmed by the project owner.
2. [Game Design](docs/GAME_DESIGN.md).
3. The implementation in `Assets/`.
4. The original GDD, which includes historical proposals.
5. Other project documentation.

If a proposed change conflicts with a confirmed rule or depends on an open
design decision, ask the owner before changing that behaviour. Record a
difference between design and implementation in the relevant section of
[Systems](docs/SYSTEMS.md).

## Working with the project

- Inspect the relevant folder, script and callers. Avoid recursive directory
  listings. Treat `Packages/` as read-only unless the task explicitly includes
  dependency changes. Skip `Library/`, `Temp/`, `obj/`, `Build/` and `Builds/`.
- Keep each Unity asset with its `.meta` file. Put new scripts beside the
  feature they implement and check for an assembly definition before adding a
  dependency across directories.
- Follow [Development Conventions](docs/CONVENTIONS.md) for C#, Inspector
  references and verification, and [Networking](docs/NETWORKING.md) for NGO
  authority and replication.

## Unity Editor

Use the configured Unity MCP server first for scenes, prefabs, GameObjects,
serialized fields, animations, import settings, Build Settings, Play Mode,
the Console and Unity tests.

1. Check Editor status and confirm the project path.
2. Use the dedicated asset, hierarchy, component or settings inspection command.
   If it cannot expose a value, use a small read-only Editor API query.
3. For authorized mutations, confirm that the target is within scope and use
   the corresponding Editor command. Keep inspection read-only.

Read C#, Markdown and text-authored configuration directly. If the Editor or
MCP is unavailable, report the limitation and identify any file-level fallback.
Do not reconstruct Unity assets from YAML, fileIDs or GUIDs with ad-hoc scripts.
Use only tools available in the session.

## Subagents

The lead agent may delegate independent parts of an authorized task. Prefer a
lower-cost model such as GPT-6.2 Sol when it fits the work. Give each subagent
specific files to edit or a read-only question to answer. Coordinate scene
changes, recompiles and Play Mode so one agent at a time drives the shared
Editor. The lead agent reviews the combined changes and verifies the result.
