# Casino art and scene authoring

## Visual direction

Preserve the owner's revised BigRoom1/roulette salon as the reference: ivory
walls, dark ceilings, petrol wainscot tiles, restrained brass, burgundy drapes,
framed paintings and metric diamond carpets. Lighting follows warm 1990s Las
Vegas: golden chandeliers/amber sconces, with turquoise on the western lower
branch and rose on the eastern branch. Visible emission follows fixture color.

Preserve the authored furniture, curtains, paintings and gameplay-object
placements. Do not restore removed cashier/signage/floating-light layouts from
old setup scripts or snapshots.

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
| `VegasLighting/Models/CasinoWallLamp_Restored.fbx` | Preserve the owner's silhouette, UVs and three material slots |

Carpet borders use non-overlapping mesh strips. Create a mesh of the required
size instead of nonuniform Transform scale, which stretches motif density.
Avoid doorway openings, unsupported floors and stair voids. Check imported
material slots by submesh names (Bronze / Ivory / Brass), not assumed Blender
slot order. Preserve asset GUIDs when revising exports.

## Lighting and navigation

Static fixtures use Baked lighting. The original salon and two main gallery
chandeliers use Mixed lighting with Shadowmask in
`VegasLighting/Casino_Vegas_Baked.lighting`. Keep lights attached to visible
fixtures; removed strip-light components must not be recreated automatically.

Static shells/decoration need valid lightmap UVs. Moving gate pieces and EyePlant
use APV. Exclude empty imported meshes from GI. After geometry or light changes,
rebake the affected lighting/APV and verify after save/reopen; previous bake
counts and reports are not acceptance criteria for a new layout.

Decoration has no active colliders and is excluded from NavMesh builds. Preserve
existing collision and navigation; adding a real obstacle requires reviewing
navigation separately. Acoustic geometry follows [Steam Audio](STEAM_AUDIO.md#геометрія-та-повторне-запікання),
which excludes decorative meshes and furniture.

## Validation

Check material assignments, pivots, carpet density/overlap, fixture alignment,
lightmap/APV validity, door/stair clearance and existing gameplay references.
Inspect both floor levels in Game View and profile on target hardware.

Tune the authored scene/prefabs in the Inspector. Local historical Casino art
construction scripts are not runtime generators: rerunning creation stages on
an authored scene can duplicate content or overwrite owner edits. Old fixed
object-count and placement checks do not describe the current room.
