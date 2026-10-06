# Persistence Development Guidance

Read the root `AGENTS.md` and `docs/SYSTEMS.md` before changing save/load
code.

## Important entry points

- `Scripts/GameFile.cs` — file abstraction and serialization contract.
- `Scripts/GameFileHandler.cs` — file discovery and load/delete orchestration.
- `Scripts/SaveFile.cs` — game progression save data.
- `Scripts/SaveGameButton.cs` and `Scripts/LoadSaveButton.cs` — manual
  save/load UI entry points.
- `Scripts/NetworkSaveables.cs` — networked saveable objects.

- Keep persistent progression separate from transient mission state unless the
  design explicitly says otherwise.
- Do not persist an `EnemyCage` until the team has successfully returned to
  the base and the result has been accepted.
- Items left in the mission zone are destroyed when the mission ends; items
  brought into the base remain available.
- Save automatically after mission completion; allow manual saves only when
  no mission is active.
- Preserve existing file discovery, load, and delete behaviour.
- Handle missing or invalid files explicitly and keep user-visible failures
  diagnosable.
