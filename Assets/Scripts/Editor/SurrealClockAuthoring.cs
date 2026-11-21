using System;
using System.Collections.Generic;
using System.IO;
using Game.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class SurrealClockAuthoring
    {
        private const string ScenePath = "Assets/Scenes/Tutorial/Tutorial.unity";
        private const string Folder = "Assets/Scenes/Tutorial";
        private const string ModelPath = Folder + "/CeilingClockDetails.fbx";

        [MenuItem("Tools/Tutorial/Install Ceiling Clock Details")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Install clock details outside Play Mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            GameObject clock = null;
            if (scene.isLoaded)
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == "CeilingClock") clock = root;
            if (clock == null)
                throw new InvalidOperationException("Open Tutorial with its authored CeilingClock first.");

            string export = Path.GetFullPath("tools/CeilingClockDetails.fbx");
            if (File.Exists(export))
            {
                File.Copy(export, ModelPath, true);
                AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
                throw new InvalidOperationException("Export ArtSource/build_clock_details.py from Blender first.");

            Dictionary<string, Material> materials = new Dictionary<string, Material>
            {
                ["Clock_Enamel"] = Surface("Enamel", new Color(.64f,.61f,.53f), .02f, .18f),
                ["Clock_Ink"] = Surface("Ink", new Color(.035f,.047f,.041f), .12f, .32f),
                ["Clock_Brass"] = Surface("Brass", new Color(.56f,.34f,.13f), .55f, .32f),
                ["Clock_Patina"] = Surface("Patina", new Color(.12f,.23f,.19f), .42f, .28f)
            };
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.RemoveRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Clock_Jade"));
            importer.RemoveRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Clock_Lume"));
            foreach (KeyValuePair<string, Material> entry in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.Key), entry.Value);
            importer.SaveAndReimport();

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Finish Tutorial CeilingClock");
            Transform previous = clock.transform.Find("DimensionalDial");
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            GameObject details = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), scene);
            details.name = "DimensionalDial";
            Undo.RegisterCreatedObjectUndo(details, "Add clock dial");
            details.transform.SetParent(clock.transform, false);
            details.transform.localPosition = Vector3.zero;
            details.transform.localRotation = Quaternion.identity;
            details.transform.localScale = Vector3.one;
            foreach (Renderer renderer in details.GetComponentsInChildren<Renderer>())
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null && materials.TryGetValue(slots[i].name, out Material material))
                        slots[i] = material;
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }

            Renderer body = Require(clock.transform, "Cylinder").GetComponent<Renderer>();
            Undo.RecordObject(body, "Refine clock housing");
            body.sharedMaterials = new[]
            {
                Surface("Walnut", new Color(.19f,.09f,.041f), .12f, .33f),
                materials["Clock_Enamel"], materials["Clock_Brass"], materials["Clock_Brass"]
            };
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            Transform first = Require(clock.transform, "BézierCurve");
            Transform second = Require(clock.transform, "BézierCurve.001");
            Transform third = Require(clock.transform, "BézierCurve.002");
            SetOriginalHand(first, 36f, materials["Clock_Ink"]);
            SetOriginalHand(second, 128f, materials["Clock_Brass"]);
            SetOriginalHand(third, -42f, materials["Clock_Patina"]);

            SurrealClockPresentation presentation = clock.GetComponent<SurrealClockPresentation>();
            if (presentation == null) presentation = Undo.AddComponent<SurrealClockPresentation>(clock);
            SerializedObject fields = new SerializedObject(presentation);
            SerializedProperty hands = fields.FindProperty("hands");
            hands.arraySize = 5;
            Configure(hands.GetArrayElementAtIndex(0), first, clock.transform.up,
                SurrealClockPresentation.Motion.Sweep, 2.2f);
            Configure(hands.GetArrayElementAtIndex(1), second, clock.transform.up,
                SurrealClockPresentation.Motion.Stepped, 0, -17f, 1.3f, .21f, .26f);
            Configure(hands.GetArrayElementAtIndex(2), third, clock.transform.up,
                SurrealClockPresentation.Motion.Rewind, 9f, phase: 2.8f,
                reversePeriod: 8.7f, reverseDuration: 1.8f, reverseDegrees: 48f);
            Configure(hands.GetArrayElementAtIndex(3), Require(details.transform, "ForkHand"), clock.transform.up,
                SurrealClockPresentation.Motion.Sweep, -4.8f);
            Configure(hands.GetArrayElementAtIndex(4), Require(details.transform, "VerdigrisNeedle"), clock.transform.up,
                SurrealClockPresentation.Motion.Stepped, 0, 11f, .83f, .16f, .30f);
            fields.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = clock;
        }

        private static Material Surface(string suffix, Color color, float metallic, float smoothness)
        {
            string path = Folder + "/Materials/Clock_" + suffix + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform Require(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null) throw new InvalidOperationException("Missing clock part: " + name);
            return child;
        }

        private static void SetOriginalHand(Transform hand, float angle, Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CeilingClock.fbx");
            Transform original = Require(source.transform, hand.name);
            Undo.RecordObject(hand, "Pose clock hand");
            hand.localRotation = Quaternion.AngleAxis(angle, Vector3.up) * original.localRotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(hand);
            Renderer renderer = hand.GetComponent<Renderer>();
            Undo.RecordObject(renderer, "Refine clock hand");
            renderer.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        private static void Configure(SerializedProperty hand, Transform pivot, Vector3 worldAxis,
            SurrealClockPresentation.Motion motion, float speed, float step = 0f,
            float interval = 1f, float travel = .2f, float phase = 0f,
            float reversePeriod = 8f, float reverseDuration = 1.5f, float reverseDegrees = 40f)
        {
            hand.FindPropertyRelative("pivot").objectReferenceValue = pivot;
            hand.FindPropertyRelative("localAxis").vector3Value = pivot.InverseTransformDirection(worldAxis).normalized;
            hand.FindPropertyRelative("motion").enumValueIndex = (int)motion;
            hand.FindPropertyRelative("degreesPerSecond").floatValue = speed;
            hand.FindPropertyRelative("stepDegrees").floatValue = step;
            hand.FindPropertyRelative("stepInterval").floatValue = interval;
            hand.FindPropertyRelative("stepTravelDuration").floatValue = travel;
            hand.FindPropertyRelative("phase").floatValue = phase;
            hand.FindPropertyRelative("reversalPeriod").floatValue = reversePeriod;
            hand.FindPropertyRelative("reversalDuration").floatValue = reverseDuration;
            hand.FindPropertyRelative("reversalDegrees").floatValue = reverseDegrees;
        }
    }
}
