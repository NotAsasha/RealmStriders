# Enemy systems

Use [Enemy](Enemy.cs) for shared monster behaviour, [EntityDetector](EntityDetector.cs)
for perception and [EnemySpawner](EnemySpawner.cs) for server-side encounter
generation. Individual monsters live in their own directories here.

[Entity](../Player/Entity.cs) owns health, status, death and capture state.
[LeafBlower](../Player/Equipment/LeafBlower/LeafBlower.cs) accumulates capture
progress. Preserve the distinction between a capture and a kill when reporting
a target's outcome to [GameManager](../GameManager.cs).

Check affected prefabs when changing serialized fields or spawn pools. Verify
host and client spawn, counterplay, capture and despawn at mission end. See
[Enemies](../../docs/ENEMIES.md) and the [monster authoring workflow](../../docs/CONTENT_PIPELINE.md#monsters).
