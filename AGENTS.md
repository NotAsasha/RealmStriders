# Unity Project Guidelines for AI Agents

## Project Scope & Architecture
- **Engine**: Modern Unity (C# 9+ / .NET Standard 2.1).
- **Networking**: Netcode for GameObjects (NGO) with Facepunch Steamworks transport.
- **Render Pipeline**: Universal Render Pipeline (URP).
- **Core Principle**: Decouple business/game logic from Unity `MonoBehaviour` where possible (pure C# classes, interfaces, events).

---

## Filesystem & Unity Asset Rules
1. **Metadata Integrity**:
   - NEVER create, move, rename, or delete assets manually without generating or updating corresponding `.meta` files.
   - If generating scripts, place them in designated directories (e.g., `Assets/Scripts/...`).
2. **Directory Priorities**:
   - `Assets/`: Primary game codebase and assets. Start searches here.
   - `Packages/`: Project dependencies. Read-only unless explicitly instructed.
   - `Library/`, `Temp/`, `obj/`, `Builds/`: Transient and auto-generated. **Completely ignore.**
3. **Assembly Definitions (asmdef)**:
   - Respect project boundaries. Check if target folders use custom `.asmdef` files before introducing cross-namespace dependencies.

---

## C# & Unity Coding Standards
- **Performance**:
  - NO `GetComponent`, `FindObjectOfType`, or `Camera.main` inside `Update()`, `FixedUpdate()`, or tight loops. Cache references in `Awake()` or initialize via injection.
  - Minimize heap allocations in frame loops (avoid LINQ, string concatenations, or boxing where possible).
- **Networking (NGO Rules)**:
  - Network-aware classes must inherit from `NetworkBehaviour`, not `MonoBehaviour`.
  - Always guard network operations with authority checks (`IsServer`, `IsClient`, `IsOwner`).
  - Use `NetworkVariable<T>` for persistent state synchronization and RPCs (`[ServerRpc]`, `[ClientRpc]`) strictly for transient events.
  - Implement cleanup/subscriptions in `OnNetworkSpawn()` and `OnNetworkDespawn()`, not standard `Start()` / `OnDestroy()`.
- **Code Style**:
  - Strongly typed, idiomatic C#.
  - Explicit access modifiers (`private`, `protected`, `public`).
  - Minimal, high-signal comments explaining *why*, not *what*.

---

## Agent Operational Constraints
- **Targeted Exploration**: Do not read directory trees recursively. Check folder structures locally around the target task.
- **No Hallucinated Tools**: Do not attempt to invoke non-standard execution tools or visual screenshot APIs unless an active Unity MCP server is explicitly loaded.
- **Verification**: Ensure all generated C# code has correct namespace imports and passes type safety checks before concluding tasks.

## Unity Editor Tooling

When a task involves Unity Editor state or Unity-serialized data, use the
configured Unity MCP server first. This includes scenes, prefab contents,
GameObjects, components, serialized fields, AnimatorControllers, import
settings, Build Settings, Play Mode, the Unity Console, and Unity tests.

Recommended read-only workflow:

1. Call `unity-editor_status` to confirm that the Editor is connected and ready.
2. Use Unity MCP inspection commands such as `unity-find_assets`,
   `unity-find_gameobjects`, `unity-get_component_properties`,
   `unity-get_serialized_fields`, `unity-get_scene_hierarchy`, or
   `unity-console` as appropriate.
3. Use `unity-eval` only for a small, read-only Editor API check when no
   dedicated inspection command exposes the required information.
4. Report when Unity MCP was unavailable instead of silently presenting a
   file-level approximation as authoritative.

Do not parse Unity YAML, `.meta` files, prefab fileIDs, or GUID mappings with
Python or ad-hoc scripts as a substitute for Unity MCP. Direct file inspection
is appropriate for C# source, Markdown, JSON configuration, and other
text-authored files. It is a fallback for Unity assets only when the Editor or
MCP connection is unavailable, and that limitation must be stated explicitly.

For mutating Unity operations, use the corresponding Unity MCP command and
confirm the intended scope before applying changes. Prefer read-only inspection
first, and never modify a scene, prefab, or asset merely to investigate it.

## Source of Truth

When sources disagree, use this priority:

1. Explicit decisions confirmed by the project owner.
2. Current gameplay design in `docs/GAME_DESIGN.md`.
3. Current implementation in `Assets/`.
4. The original GDD, which may contain obsolete proposals.
5. Other project documentation.

If a requested change conflicts with a confirmed design rule or affects an
unresolved rule, stop and ask for clarification before editing. If the code
and confirmed design disagree, describe the implementation gap rather than
silently treating the code as the intended behaviour.

## Project Documentation

Before changing gameplay or architecture, consult the relevant documentation:

- [Game design](docs/GAME_DESIGN.md) — core loop, design pillars, and open questions.
- [Systems overview](docs/SYSTEMS.md) — system boundaries and responsibilities.
- [Networking](docs/NETWORKING.md) — authority, state, and RPC guidance.
- [Enemies](docs/ENEMIES.md) — enemy content and spawn conventions.
- [Development conventions](docs/CONVENTIONS.md) — implementation and asset rules.
- [Content pipeline](docs/CONTENT_PIPELINE.md) — how to add monsters, equipment, and missions.

These documents describe the current state. Use the source-of-truth priority
above when code and documentation disagree. Record implementation gaps instead
of silently treating current code as intended design.