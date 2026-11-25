# Content Pipeline

Follow the [design](GAME_DESIGN.md) and [development conventions](CONVENTIONS.md).

## Monsters

1. Define role, danger, detection, attacks, equipment counterplay and failure
   cases in [Enemies](ENEMIES.md).
2. Reuse shared enemy abstractions and implement specialized behaviour in the
   monster's folder. Identify stackable weaknesses and server-owned state.
3. Configure the prefab, navigation, animation, effects and separate step/vocal
   sources. Register it in NGO and the appropriate enemy danger pool.
   If it belongs in End, add its prefab explicitly to EnemySpawner's
   `finalMissionEnemies` on GameManager; keep that roster unique and within the
   mission payload capacity. The End preview and spawn both use this list.
4. Configure explicit EnemyAcoustics emitter pairs as described in
   [Steam Audio](STEAM_AUDIO.md#вороги-та-нові-джерела).
5. Test host/client spawn, capture, player death and mission-end despawn.

## Equipment

1. Define whether it captures, weakens, detects or supports, and which enemies
   it affects. Reuse existing item/equipment interfaces.
2. Register the network prefab and follow the [network authority rules](NETWORKING.md).
3. Assign visuals, effects and the `Item.icon` inventory sprite. If `clickSounds`
   is populated, assign an enabled AudioSource on the Item root with Play On
   Awake disabled; a child source does not supply Item's default use click.
4. Follow [Held items](HELD_ITEMS.md#authoring) for grip, poses, camera bounds
   and inventory presentation. For spatial sources, see [Steam Audio](STEAM_AUDIO.md#предмети-та-переносні-ефекти).
5. Test stacking, capture timing and charge as applicable, then run the
   [held-item checks](HELD_ITEMS.md#verification).

Assign every registered saveable prefab, including each variant, a unique
`Prefab Id` on its Item or Terminal component. Keep existing IDs when reordering
the `NetworkSaveables` list and reserve retired IDs so old slots can never load a
different prefab for the same ID.

## Shop previews

Item cards use mesh-only prefabs under `Assets/Base/Shop/Previews/`. Assign Shop,
catalog, Buy button, shared ShopItemPreview, preview RawImage and model explicitly.
Set the category's required rating to 0, 4, 7, 10 or 13. Server purchases use the
card's configured price and requirement.

After a mesh change, update the preview with centered/scaled mesh renderers and
materials. Bake ProBuilder geometry if needed. Exclude gameplay scripts,
particles, colliders, lights and NetworkObjects. Verify detail and quick-buy
flows, preview shutdown and the locked-category state.

## Crafting recipes

Add an entry to `Crafter.Recipes` with two input Item prefabs and one output
prefab. Input order does not matter. Configure `Craft Text` for the preview;
empty text falls back to input/output names. Upgraded variants must retain
distinct identities; do not match ingredients by display name.

Dynamic inputs use NGO prefab hashes. Scene inputs resolve `Item.PrefabId`
through `NetworkItemsHandler.database`; missing registry data rejects them.
Assign socket triggers, output point, doors, indicators, audio, particles and
preview labels explicitly. Connect the physical button to `TryStartCraft`.
Check both ingredient orders, wrong variants, pickup from an occupied socket,
output spawn and consistent host/client preview/effects.

## Worlds and missions

1. Define the objective and return outcome within the confirmed mission rules.
2. Configure the scene, spawn references, portal, navigation and mission selection;
   add the scene to the enabled build list.
3. Verify server additive loading, enemy placement outside excluded portal/base
   areas and unloading with a host and a client.
4. Test capture/return, early closure, timer expiry, team death and disconnects,
   including both returned and unreturned held/stored items.
5. For acoustic geometry, probes and rebaking follow
   [Steam Audio](STEAM_AUDIO.md#геометрія-та-повторне-запікання).
6. Casino art changes also follow [Casino art](CASINO_ART.md).

## Tutorial ceiling clock

The Tutorial's `CeilingClock` has a local `SurrealClockPresentation`. Assign each
hand pivot and its local rotation axis explicitly in the Inspector. Sweep uses
degrees per second; Stepped uses step angle, interval and travel duration; Rewind
adds a periodic backward excursion. Phase offsets are seconds. Keep this ambient
clock independent of tutorial progress and mission timing.

Keep the dial free of numbers, lettering, emblems and secondary clocks. Use
chunky geometry and matte solid colors that fit the Tutorial's existing lamps
and painterly rendering. Floating arc fragments have separate centers and tilted
planes; the clock uses no texture maps.

Keep `Assets/Scenes/Tutorial/CeilingClockDetails.fbx` separate from the original
housing FBX and preserve its mesh names and `Clock_*` material slots on export.
With Tutorial open outside Play Mode, **Tools → Tutorial → Install Ceiling Clock
Details** restores the shipped materials, poses and motion defaults. It uses the
versioned model or imports a replacement from `tools/CeilingClockDetails.fbx` if
present. Save the scene after running it.

## Shared Field and Menu environment

Edit common buildings, pipes and background lights in
`Assets/Enviroment/FieldEnvironment.prefab`. It retains editable ProBuilder
components. After editing geometry, run **Tools → Worlds → Refresh Shared Field
Meshes** to update the persistent meshes used by both scenes, then rebake Field
lighting, navigation and acoustics as needed. Scene instances intentionally remove
the ProBuilder components and reference the shared mesh library; keep those
component removals on the instances when editing the source prefab.

Menu-specific props stay outside the shared geometry or as instance additions.
Keep APV generation in Field. Menu uses its existing lightmaps and direct lighting
for its fixed camera and must not acquire a scene-wide APV baking set. Preserve
the camera's parallax margin when editing the visible backdrop.

Field and Menu Terrain objects have `TerrainGraphicsQuality` with an explicit
Terrain reference. Author the High Fidelity density/distance on the Terrain and
the lower-tier multipliers on that component. Keep grass detail prototypes'
`Use Density Scaling` enabled so density settings can affect them. Do not reduce
the shared TerrainData's painted detail layers to implement a quality tier.

When adding a quality tier, keep `URP-Radar-Renderer` at renderer index 1 and
the scene renderer at index 0. Radar cameras use index 1 with post-processing
and shadows disabled. Its RenderObjects feature preserves RadarOnly markers;
do not remove that pass with the scene-only fullscreen effects. Assign Radar's
screenRenderer explicitly to the display Plane (front normal local +Y), and
configure visibilityOccluders to match structural collision layers.

## Shader warmup

### Shader variant reduction

Keep **Strip Unused Post Processing Variants** disabled in the URP Graphics
settings. With the current volume setup, enabling it removes a post-processing
variant required by Lobby Bloom in the Player, despite the effect working in
Editor Play Mode. Regular **Strip Unused Variants** remains enabled. When
revisiting post-process stripping, compare Bloom on/off in an actual Player;
the presence of Bloom.shader in the build alone does not prove the required
UberPost variant survived.

Graphics > Shader Build Settings overrides `_ FOG_LINEAR FOG_EXP FOG_EXP2` to
`dynamic_branch`. Fog modes remain available without separate shader variants.
Custom shaders using this keyword set must support dynamic branching; use URP
fog functions or HLSL runtime conditionals rather than `#if` on fog keywords.

All three quality URP assets disable light cookies and LOD cross-fading because
the current build scenes and authored prefabs use neither feature. If adding a
Light cookie or a cross-fading LODGroup, enable its support in the relevant URP
assets before testing a player build. Keep APV support and its baking sets intact.

### Desktop light limit

`Packages/com.unity.render-pipelines.universal-config` is an embedded copy of
the installed 17.0.3 config package. Desktop visible lights are capped at 64
to reduce URP Lit shader compilation cost. Keep
`ShaderOptions.k_MaxVisibleLightCountDesktop` in `Runtime/ShaderConfig.cs` and
`MAX_VISIBLE_LIGHT_COUNT_DESKTOP` in `Runtime/ShaderConfig.cs.hlsl` equal.
Mobile limits remain unchanged. Forward+ includes the main light in this cap;
lights beyond the per-camera limit can be omitted. Check the busiest lighting
scenes when changing it. Restart the Editor after changing this configuration.

Embedded package sources are versioned and do not update automatically with
Unity/URP. On upgrades, compare the new upstream config package, retain its
metadata, reapply the desktop cap and verify both C# and generated HLSL values.

### Variant collection and validation

When retiring a renderer effect, remove its unused material/shader references
or remove the renderer feature. Disabling execution alone can leave shader
dependencies in the build. Check `AssetDatabase.GetDependencies` from the enabled
build scenes and included URP assets, plus Resources and explicit shader lists.
Do not remove a shader still used by a material or runtime effect.

When adding a custom effect or changing its shader keywords, update the explicit
variants in `Assets/Scripts/Editor/GameplayShaderWarmupAuthoring.cs`, then run
**Tools → Rendering → Rebuild Gameplay Shader Variants** outside Play Mode.
The same generator runs before builds and fails on missing shaders, shader
errors or invalid pass/keyword combinations. Commit the generated
`Assets/Resources/GameplayShaderVariants.shadervariants` and its metadata.
Use `ScriptableRenderPipeline` for `UniversalForward` passes and `Normal` for
the untagged effect/fullscreen passes. Do not use Built-in `ForwardBase` for URP.
The collection includes the declared orbital fog/instancing combinations and
volumetric-fog shadow modes; keep these in sync with their shader pragmas.

Verify a cold start and first use of each effect in a Development Player on each
target graphics API, with the Profiler checking `Shader.CreateGPUProgram` and
`CreateGraphicsGraphicsPipelineImpl`. Editor caches alone do not prove coverage.
Shader variant warmup reduces first-use compilation; on DX12, Vulkan and Metal,
exact vertex layouts/render states can still require additional PSOs. Trace and
warm a representative GraphicsStateCollection on target hardware if profiling
shows remaining shader stalls.
