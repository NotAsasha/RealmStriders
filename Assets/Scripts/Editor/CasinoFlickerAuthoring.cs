using System;
using System.Collections.Generic;
using Game.Casino;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class CasinoFlickerAuthoring
    {
        private const string ScenePath = "Assets/Scenes/Casino/Casino.unity";
        private const string MaterialFolder = "Assets/Scenes/Casino/LastWager/VegasLighting";
        private static readonly string[] Rooms = { "SimpleCorridor1", "SimpleTurn1", "SimpleThreeway" };
        private static readonly string[] SelectedInstances =
        {
            "SimpleCorridor1 (2)", "SimpleCorridor1 (8)", "SimpleCorridor1 (17)", "SimpleCorridor1 (24)",
            "SimpleTurn1 (1)", "SimpleTurn1 (4)", "SimpleTurn1 (11)",
            "SimpleThreeway (5)", "SimpleThreeway (15)"
        };

        [MenuItem("Tools/Casino/Validate Mission Light Flicker")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying || Lightmapping.isRunning)
                throw new InvalidOperationException("Validate outside Play Mode after baking.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            HashSet<uint> hashes = new();
            int count = 0;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var hashField = typeof(NetworkObject).GetField("GlobalObjectIdHash",
                flags | System.Reflection.BindingFlags.Public);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MissionLightFlicker fixture in root.GetComponentsInChildren<MissionLightFlicker>(true))
                {
                    SerializedObject fields = new SerializedObject(fixture);
                    Light light = (Light)fields.FindProperty("targetLight").objectReferenceValue;
                    Renderer renderer = (Renderer)fields.FindProperty("emissionRenderer").objectReferenceValue;
                    int slot = fields.FindProperty("emissionMaterialIndex").intValue;
                    if (light == null || renderer == null || light.lightmapBakeType != LightmapBakeType.Realtime ||
                        renderer.sharedMaterials[slot].globalIlluminationFlags != MaterialGlobalIlluminationFlags.None)
                        throw new InvalidOperationException("Invalid flicker fixture: " + fixture.name);
                    uint hash = (uint)hashField.GetValue(fixture.GetComponent<NetworkObject>());
                    if (hash == 0 || !hashes.Add(hash))
                        throw new InvalidOperationException("Missing or duplicate scene NetworkObject hash.");
                    count++;
                }
            if (count != SelectedInstances.Length) throw new InvalidOperationException("Expected nine scene fixtures.");

            foreach (string room in Rooms)
            {
                GameObject root = PrefabUtility.LoadPrefabContents("Assets/Scenes/Casino/Rooms/" + room + "_Flicker.prefab");
                try
                {
                    MissionLightFlicker fixture = root.GetComponentInChildren<MissionLightFlicker>(true);
                    SerializedObject fields = new SerializedObject(fixture);
                    Light light = (Light)fields.FindProperty("targetLight").objectReferenceValue;
                    Renderer renderer = (Renderer)fields.FindProperty("emissionRenderer").objectReferenceValue;
                    int slot = fields.FindProperty("emissionMaterialIndex").intValue;
                    float intensity = light.intensity;
                    Color emission = renderer.sharedMaterials[slot].GetColor("_EmissionColor");
                    fixture.OnNetworkSpawn();
                    typeof(MissionLightFlicker).GetField("burstStart", flags).SetValue(fixture, 10d);
                    typeof(MissionLightFlicker).GetField("currentBurstDuration", flags).SetValue(fixture, 1f);
                    typeof(MissionLightFlicker).GetField("burstSeed", flags).SetValue(fixture, 12345u);
                    var update = typeof(MissionLightFlicker).GetMethod("UpdatePresentation", flags);
                    bool darkened = false;
                    MaterialPropertyBlock block = new();
                    for (int pulse = 0; pulse < 10; pulse++)
                    {
                        update.Invoke(fixture, new object[] { 10d + pulse * 0.09d });
                        renderer.GetPropertyBlock(block, slot);
                        float brightness = light.intensity / intensity;
                        Color expected = emission * brightness;
                        if (block.GetColor("_EmissionColor") != expected)
                            throw new InvalidOperationException("Emission and direct light diverged.");
                        if (!light.enabled && light.intensity == 0f) darkened = true;
                    }
                    update.Invoke(fixture, new object[] { 12d });
                    renderer.GetPropertyBlock(block, slot);
                    if (!darkened || !light.enabled || light.intensity != intensity || block.GetColor("_EmissionColor") != emission)
                        throw new InvalidOperationException("Burst did not darken and restore " + room);
                    var apply = typeof(MissionLightFlicker).GetMethod("ApplyBrightness", flags);
                    apply.Invoke(fixture, new object[] { 0f });
                    typeof(MissionLightFlicker).GetMethod("OnDisable", flags).Invoke(fixture, null);
                    if (!light.enabled || light.intensity != intensity)
                        throw new InvalidOperationException("Disable did not restore " + room);
                    apply.Invoke(fixture, new object[] { 0f });
                    fixture.OnNetworkDespawn();
                    renderer.GetPropertyBlock(block, slot);
                    if (!light.enabled || light.intensity != intensity || block.GetColor("_EmissionColor") != emission)
                        throw new InvalidOperationException("Despawn did not restore " + room);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("Casino flicker validation passed: nine unique scene identities; all variants dim and restore light/emission. This does not replace a host/client transport playtest.");
        }

        [MenuItem("Tools/Casino/Configure Mission Light Flicker")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open Casino before configuring flicker.");

            foreach (string room in Rooms)
            {
                string path = "Assets/Scenes/Casino/Rooms/" + room + "_Flicker.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    MissionLightFlicker existing = root.GetComponentInChildren<MissionLightFlicker>(true);
                    Light light = existing != null
                        ? (Light)new SerializedObject(existing).FindProperty("targetLight").objectReferenceValue
                        : root.GetComponentsInChildren<Light>(true)[0];
                    if (light == null) throw new InvalidOperationException("Missing light in " + path);
                    GameObject fixture = light.transform.parent.gameObject;
                    Renderer renderer = fixture.GetComponentInChildren<Renderer>(true);
                    if (renderer == null) throw new InvalidOperationException("Missing fixture renderer in " + path);
                    Material[] materials = renderer.sharedMaterials;
                    int slot = Array.FindIndex(materials, m => m != null && m.IsKeywordEnabled("_EMISSION") &&
                        m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0f);
                    if (slot < 0) throw new InvalidOperationException("Missing emissive material in " + path);
                    Material source = materials[slot];
                    string materialPath = MaterialFolder + "/" + source.name.Replace("_Flicker", "") + "_Flicker.mat";
                    Material dynamicMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (dynamicMaterial == null)
                    {
                        dynamicMaterial = new Material(source);
                        dynamicMaterial.name = source.name + "_Flicker";
                        AssetDatabase.CreateAsset(dynamicMaterial, materialPath);
                    }
                    dynamicMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    EditorUtility.SetDirty(dynamicMaterial);
                    materials[slot] = dynamicMaterial;
                    renderer.sharedMaterials = materials;

                    light.lightmapBakeType = LightmapBakeType.Realtime;
                    light.shadows = LightShadows.None;
                    NetworkObject networkObject = fixture.GetComponent<NetworkObject>();
                    if (networkObject == null) networkObject = fixture.AddComponent<NetworkObject>();
                    networkObject.SynchronizeTransform = false;
                    MissionLightFlicker flicker = fixture.GetComponent<MissionLightFlicker>();
                    if (flicker == null) flicker = fixture.AddComponent<MissionLightFlicker>();
                    SerializedObject settings = new SerializedObject(flicker);
                    settings.FindProperty("targetLight").objectReferenceValue = light;
                    settings.FindProperty("emissionRenderer").objectReferenceValue = renderer;
                    settings.FindProperty("emissionMaterialIndex").intValue = slot;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            List<GameObject> selected = new();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (Array.IndexOf(SelectedInstances, child.name) >= 0) selected.Add(child.gameObject);
            if (selected.Count != SelectedInstances.Length)
                throw new InvalidOperationException("Expected all nine authored room instances.");
            foreach (GameObject room in selected)
            {
                string module = room.name.Substring(0, room.name.IndexOf(' '));
                GameObject variant = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Scenes/Casino/Rooms/" + module + "_Flicker.prefab");
                if (PrefabUtility.GetCorrespondingObjectFromSource(room) == variant) continue;
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(room, variant, new PrefabReplacingSettings
                {
                    objectMatchMode = ObjectMatchMode.ByHierarchy,
                    prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
                    changeRootNameToAssetName = false
                }, InteractionMode.AutomatedAction);
            }

            // Scene instances contain intentional art overrides. Touch only the flickering fixture.
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MissionLightFlicker flicker in root.GetComponentsInChildren<MissionLightFlicker>(true))
                {
                    string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(flicker);
                    if (!sourcePath.EndsWith("_Flicker.prefab", StringComparison.Ordinal)) continue;
                    SerializedObject fields = new SerializedObject(flicker);
                    Light light = (Light)fields.FindProperty("targetLight").objectReferenceValue;
                    Renderer renderer = (Renderer)fields.FindProperty("emissionRenderer").objectReferenceValue;
                    int slot = fields.FindProperty("emissionMaterialIndex").intValue;
                    Undo.RecordObject(light, "Configure Casino flickering light");
                    light.lightmapBakeType = LightmapBakeType.Realtime;
                    light.shadows = LightShadows.None;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(light);

                    Material[] materials = renderer.sharedMaterials;
                    Material original = materials[slot];
                    string materialPath = MaterialFolder + "/" + original.name.Replace("_Flicker", "") + "_Flicker.mat";
                    Material dynamicMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (dynamicMaterial == null)
                    {
                        dynamicMaterial = new Material(original);
                        dynamicMaterial.name = original.name + "_Flicker";
                        dynamicMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                        AssetDatabase.CreateAsset(dynamicMaterial, materialPath);
                    }
                    Undo.RecordObject(renderer, "Configure Casino flickering emission");
                    materials[slot] = dynamicMaterial;
                    renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    count++;
                }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Casino flicker configured on " + count + " scene fixtures. Rebake Casino lighting/APV.");
        }
    }
}
