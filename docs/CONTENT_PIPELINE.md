# Content Pipeline

This is the default workflow for adding gameplay content. If a new feature
requires a different process, document the exception and its reason.

## Adding a monster

1. Define the monster's role, danger level, detection, attack, capture
   behaviour, counterplay, and failure cases in `docs/ENEMIES.md`. Every
   target monster must be capturable by `LeafBlower`; killing is not an
   alternative mission objective.
2. Identify which equipment weakens it and whether those weaknesses stack.
3. Implement shared behaviour in the existing enemy abstractions where
   possible; keep monster-specific rules in the monster's folder.
4. Create or update the prefab, animation, sounds, and required effects,
   preserving `.meta` files.
5. Register the prefab in the appropriate enemy spawn pool.
6. Verify server-authoritative spawn, capture, despawn, and mission-end
   behaviour with a host and a client.
7. Update the design and systems documentation if the player loop or
   authority model changed.

## Adding equipment

1. Define the player's intended use and the monsters it can affect.
2. Decide whether it directly captures, weakens, detects, or supports the
   team.
3. Reuse the existing equipment interfaces and item flow.
4. Validate client requests on the server before applying gameplay effects.
5. Create the prefab, visuals, audio, and UI entry as needed.
6. Test stacking with other equipment and verify that capture timing remains
   understandable.

## Adding a world or mission

1. Define the mission objective and return-to-base outcome.
2. Add the scene and required spawn, portal, and navigation references.
3. Verify local and networked scene loading.
4. Test enemy placement outside the base and portal exclusion areas.
5. Test a successful return, a failed mission, player death, and disconnect
   behaviour.

## Definition of done

Content is not complete until implementation, prefab/scene references,
network authority, player-facing counterplay, and documentation agree.
