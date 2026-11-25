# Casino art and scene authoring

## Visual direction

Use the BigRoom1 roulette salon as the visual reference. It pairs ivory walls
and dark ceilings with petrol wainscot tiles, brass, burgundy drapes, framed
paintings and metric diamond carpets. Golden chandeliers and amber sconces
give it a warm 1990s Las Vegas palette. The western lower branch uses turquoise
and the eastern branch uses rose. Match visible emission to each fixture.

Preserve the authored placements of furniture, curtains, paintings and gameplay
objects.

## Assets and pivots

Reusable models, materials, textures and prefabs live under
`Assets/Scenes/Casino/LastWager/`:

| Area / module | Authoring rule |
| --- | --- |
| `WholeScene/` | Cornice tiles, carpet-size meshes and architectural decoration |
| Petrol panels | Bottom-center pivot; decorative face points local −Z |
| Cornice tiles | Centered pivot; 1.5 m along local X; keep sections independently editable |
| Diamond carpets | Centered, unit scale; metric UVs repeat the motif every 1.2 m |
| `VegasLighting/Casino_OrnateWallLamp.prefab` | Wall-mount-center pivot, identity root, geometry toward local +Z |
| [Restored wall lamp](../Assets/Scenes/Casino/LastWager/Models/CasinoWallLamp_Restored.fbx) | Preserve its silhouette, UVs and three material slots |

Carpet borders use non-overlapping mesh strips. Create a mesh of the required
size instead of nonuniform Transform scale, which stretches motif density.
Avoid doorway openings, unsupported floors and stair voids. Check imported
material slots by submesh names (Bronze / Ivory / Brass), not assumed Blender
slot order. Preserve asset GUIDs when revising exports.

## Lighting and navigation

Static fixtures use Baked lighting. The original salon and two main gallery
chandeliers use Mixed lighting with Shadowmask in
`VegasLighting/Casino_Vegas_Baked.lighting`. Keep lights attached to visible
fixtures.

Passage modules `SimpleCorridor1`, `SimpleTurn1` and `SimpleThreeway` use dimmer
wall lights (Intensity 0.44; the stronger Threeway fixture uses 0.5) to contrast
with the warmer, brighter rooms. Flicker variants inherit these intensities.

`SimpleCorridor1_Flicker`, `SimpleTurn1_Flicker` and `SimpleThreeway_Flicker`
are room variants with one Realtime, unshadowed wall lamp each. Use these
sparingly in the scene; the other fixtures retain baked lighting. Their
`MissionLightFlicker` has explicit Light, Renderer and emissive material-slot
references. Dedicated `_Flicker` materials retain the fixture color but exclude
emission from baked GI. Rebake lighting/APV after changing variant placement.
Tune burst timing, intensity and the spatial LampFlicker source in the Inspector.
With Casino open outside Play Mode, `Tools > Casino > Configure Mission Light
Flicker` reapplies the authored selection while retaining room art overrides.
Rebake after running it.
`Tools > Casino > Validate Mission Light Flicker` checks scene identities,
configured references, pulse brightness/emission and disable/despawn recovery
using isolated prefab contents. Also test with a host and client through Steam.

Static shells/decoration need valid lightmap UVs. Moving gate pieces and EyePlant
use APV. Exclude empty imported meshes from GI. After geometry or light changes,
rebake the affected lighting/APV and verify after saving and reopening the scene.

Keep decoration outside collision and NavMesh builds. Review navigation when
adding an obstacle. Follow the [acoustic export workflow](STEAM_AUDIO.md#геометрія-та-повторне-запікання).

## Validation

Check material assignments, pivots, carpet density/overlap, fixture alignment,
lightmap/APV validity, door/stair clearance and existing gameplay references.
Inspect both floor levels in Game View and profile on target hardware.
