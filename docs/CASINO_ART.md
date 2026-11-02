# Casino art direction and scene authoring

Casino now follows the owner's revised BigRoom1: ivory walls, dark ceilings,
petrol wainscot tiles, restrained brass, burgundy drapes, framed paintings and
metric diamond carpets. The original roulette salon remains the reference.
The visual pass covers 65 additional room/corridor modules and the entrance
hall, grouped by room under `Casino_WholeScene_Art`. The overlapping original
corridor instances receive one decoration set at their shared footprint.

The owner's `Casino_LastWager_Art` arrangement is preserved: roulette and
banquette transforms, two large curtain compositions, paintings, carpet and
31 remaining panel tiles. Removed cashier/signage/floating lights were not
restored. Existing billiards, columns, shelves, piano and gameplay objects in
other rooms remain in place. The two large gallery paintings are framed by
larger drapes instead of receiving another painting over their surface.

## Assets and editable modules

`Assets/Scenes/Casino/LastWager/` contains the imported models, materials,
textures and original reusable prefabs. `WholeScene/` contains the new cornice,
carpet-size meshes, lamp material and low-poly lamp prefab. Assets and their
metadata were created through Unity Editor APIs.

| Module | Triangles | Pivot / usage |
| --- | ---: | --- |
| Petrol tile 150 / 140 | 60 | Bottom centre; decorative front local -Z |
| Casino_Cornice_150 | 12 | Mesh centre; 1.5 m along local X |
| Casino_OrnateWallLamp (current) | 840 | Wall mounting point; front local +Z; Blender Decimate .3 |
| Casino_ArtDecoSconce (retired variant) | 120 | Previous simplified design, available as an asset |
| Diamond carpet | 34 | Centre; unit scale; 1.2 m texture repeat |
| Roulette table | 2,722 | Original owner-revised FBX and placements |
| Banquette | 444 | Original owner-revised FBX |
| Curtain pair | 904 | Original owner-revised FBX |
| Cashier booth | 574 | Available prefab; absent from reference room |

The original Cornice mesh used map-space vertices and a pivot at map origin.
Its scene object now has three independently movable, centred mesh sections;
its group pivot is inside the reference room. World geometry is unchanged
within 0.000005 m. New cornices use independent 1.5 m prefab tiles. Panels
remain individually editable prefab instances, including their brass trim.

New scene content: 932 panels, 932 cornice tiles, 60 carpets, 16 curtain pairs,
14 small framed paintings, 224 sconces and three entrance chandeliers.
The current ornate sconce restores the owner's preferred Blender silhouette.
Its existing Decimate modifier reduces the exported mesh to 840 triangles,
compared with the earlier 2,796-triangle export. All 222 whole-scene sconces
and four older wall lamps use the restored mesh; two colored salon sconces
were added. The retired 120-triangle design is no longer placed in the scene.
The updated whole-scene decoration root totals 278,568 triangles.

Carpet borders consist of non-overlapping mesh strips (22 cm total width).
UVs are derived from metres, with one diamond motif per 1.2 m. Different room
sizes use separate meshes at unit scale, so the pattern is not stretched.
The scene has 61 carpets including the owner's salon carpet; none overlap.
Placement checks avoid door openings, unsupported floors and stair voids.
Do not resize carpets with nonuniform Transform scale if motif density must
remain constant; generate a mesh with the desired dimensions instead.

## Models and source files

The four prop FBXs retain the owner's Blender edits from
`tools/casino-art/revision-01/casino-user-edits-snapshot.blend`.
The export script combines evaluated roulette parts only for export and does
not regenerate the owner's source. Roulette slot seven uses
`Casino_RouletteNumbers` and the owner's opaque 1024 px sRGB texture.
Original model and prefab GUIDs are retained. The first-generation Blender
source retains planar Decimate modifiers; the added architectural meshes and
sconce are authored directly at low triangle counts.

## Baked Vegas lighting revision

The lighting direction is warm 1990s Las Vegas: golden chandeliers and amber
brass sconces dominate, with rose and turquoise highlights on selected
walls and branch corridors. Visible glass emission matches each light's
color. The western lower corridor branch uses turquoise, the eastern branch
rose, and the central routes remain golden. Two wall-mounted colored lights
accent the original roulette salon without changing its furniture layout.

There are 240 active Baked lights. All 80 `Strip practical light` components
were removed at the owner's request; the strip fixtures retain their visible
geometry and material emission. Only the original roulette salon chandelier and the two main gallery
chandeliers use Mixed lighting, with Shadowmask selected in the dedicated
`VegasLighting/Casino_Vegas_Baked.lighting` settings asset. No active static
fixture uses Realtime mode. Existing inactive gate effects remain unchanged.
All sources are attached to visible lamp geometry; no floating accent lights
were added. Baked soft shadows do not add runtime shadow maps.

Lightmap UVs are generated on the participating model importers and generated
meshes. Static room shells and decoration receive lightmaps; moving gate
pieces and EyePlant renderers use the existing APV system. Empty BezierCurve
meshes in the painting FBX are excluded from GI because they contain no
vertices. The URP renderer and global pipeline settings are unchanged.

The restored lamp is `VegasLighting/Casino_OrnateWallLamp.prefab`, referencing
`Models/CasinoWallLamp_Restored.fbx`. Its root is identity, pivot is the wall
mounting centre, and geometry occupies local +Z. The export preserves the
source's three material slots and UVs. Editable source snapshot, export
script and roundtrip validation are under `tools/casino-art/vegas-lighting/`.
The user's live Blender file, modifiers and selection were left unchanged.

The previous scene and original baked assets were backed up before rebaking:
`vegas-lighting/Casino-before-lighting.unity` and
`vegas-lighting/Casino-previous-bake.unitypackage`.
`tools/CasinoVegasLighting.cs` records restoration, color assignment, UV/GI
preparation and validation. `unity-lighting-report.json` records the final
light modes, UV coverage and lamp pivot checks. Target-hardware FPS has not
been measured.

Final bake completed successfully: 21 directional lightmap atlases at
1024 px, 20 texels/m, covering 2,586 renderers. All 240 remaining static lights have
valid baked output. APV cell data and probe occlusion data were regenerated.
Material slot mapping is checked against Unity's imported submesh names
(Bronze / Ivory / Brass), rather than assuming Blender's slot order.
The final bake produced no new Console warnings or errors. Save/reopen
validation passed for lightmaps, UVs, light modes, lamp pivots and slots,
owner transforms, all 216 existing colliders and navigation references.

The added decoration has no active colliders and is excluded from future
NavMesh builds. All 216 existing collider shapes, transforms and enabled
states match the pre-pass backup. Existing NavMeshData references are
unchanged. This pass adds no furniture obstacles or runtime scripts and
requires no navigation rebake. This is preservation verification, not a new
claim about the owner's pre-existing navigation connectivity.

## Verification and review

The following passed through Unity MCP after saving and reopening Casino:

- `CasinoWholeSceneReview.SaveReport`: no missing materials, invalid panel or
  cornice pivots, overlapping carpets, unexpected runtime scripts, active
  decorative colliders or lights without nearby fixture geometry. All new
  carpet UVs and surface areas pass metric and overlap checks.
- `CasinoWholeSceneAudit.VerifyPreservation`: existing colliders, owner
  placements, 31 owner panels and navigation references match the snapshot;
  the original Cornice's world bounds are preserved.
- Twelve isolated camera views cover the salon, corridors, small rooms,
  galleries, entrance and both stair levels. No review cameras remain.

Reports: `tools/casino-art/whole-scene/summary.json`,
`structural-review.json` and `preservation-audit.json`.
Previews: `tools/casino-art/whole-final-[0-5].png` and
`whole-final-upper-[0-5].png`.
Backup: `tools/casino-art/whole-scene/Casino-owner-before.unity`.
These are the earlier art-pass images. Current baked-lighting previews are
`vegas-final-[0-3].png` (rose branch, turquoise branch, gallery, salon),
`vegas-baked-[0-5].png`, `vegas-upper-[0-5].png` and `vegas-salon-[0-3].png`
in the same directory. Lighting preservation results are in
`vegas-lighting/preservation-audit.json`.

`tools/CasinoWholeScene.cs` records one-time authoring stages: Apply,
CompleteLargeRooms, FinishDetails, OptimizeAndPolish, PolishGallery and
PersistOverrides. These are construction records, not runtime generators;
do not rerun creation stages on an already authored scene. Tune the resulting
prefabs and scene instances in the Inspector. Earlier CasinoArtSetup,
CasinoArtRevision and CasinoArtChecks scripts describe historical layouts;
their fixed-count/position checks do not represent the owner's current room.

Play Mode, multiplayer sessions and GPU profiling were not run. The initial
whole-scene pass retained the old bake; the subsequent Vegas revision
explicitly regenerates baked lighting and APV data.
