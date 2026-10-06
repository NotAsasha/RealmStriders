# Enemy Development Guidance

Read the root `AGENTS.md`, `docs/ENEMIES.md`, and `docs/NETWORKING.md` before
changing enemy code or prefabs.

## Important entry points

- `Enemy/Enemy.cs` — shared enemy state and capture lifecycle.
- `Enemy/EnemySpawner.cs` — server-side spawning and danger rolls.
- `Enemy/EntityDetector.cs` — entity detection used by enemy interactions.
- `Enemy/<Monster>/<Monster>.cs` — monster-specific behaviour.
- `Player/Entity.cs` — shared capture, death, health, and weakness state.
- `Player/Equipment/LeafBlower/LeafBlower.cs` — capture progress and weakness
  calculation.

- Enemy spawning and capture outcomes are server-authoritative.
- Every target must be captured with `LeafBlower`; other equipment weakens
  enemies and stacked weaknesses reduce capture time.
- Killing an enemy is not an alternative mission objective.
- Enemies may enter the base during an active mission; do not assume the base
  is a safe zone.
- Preserve the shared enemy abstractions unless a monster-specific rule is
  genuinely required.
- Changes to serialized fields require checking every affected prefab.
- Test with a host and at least one client, including mission-end despawn.
