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