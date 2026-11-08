#if UNITY_EDITOR && STEAMAUDIO_ENABLED
using System;
using System.Collections.Generic;
using SteamAudio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SerializedObject = UnityEditor.SerializedObject;
using Vector3 = UnityEngine.Vector3;

namespace Game.Audio.Editor
{
    public static class ProbeCoverageAuthoring
    {
        [MenuItem("Tools/Audio/Fill Selected Probe Gaps")]
        public static void FillSelected()
        {
            var batch = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<SteamAudioProbeBatch>() : null;
            if (Application.isPlaying || batch == null || batch.asset == null || Baker.IsBakeActive() || SceneManager.sceneCount != 1)
                throw new InvalidOperationException("Select a generated Probe Batch in the single open scene, in Edit Mode, after any bake finishes.");
            var serialized = new SerializedObject(batch);
            var array = serialized.FindProperty("mProbeSpheres");
            var probes = new List<Sphere>(array.arraySize);
            for (int i = 0; i < array.arraySize; i++)
            {
                var p = array.GetArrayElementAtIndex(i);
                probes.Add(new Sphere { center = new SteamAudio.Vector3 {
                    x = p.FindPropertyRelative("center.x").floatValue,
                    y = p.FindPropertyRelative("center.y").floatValue,
                    z = p.FindPropertyRelative("center.z").floatValue },
                    radius = p.FindPropertyRelative("radius").floatValue });
            }
            int before = probes.Count;
            var nav = UnityEngine.AI.NavMesh.CalculateTriangulation();
            for (int i = 0; i < nav.indices.Length; i += 3)
            {
                Vector3 floor = (nav.vertices[nav.indices[i]] + nav.vertices[nav.indices[i + 1]] + nav.vertices[nav.indices[i + 2]]) / 3f;
                Vector3 local = batch.transform.InverseTransformPoint(floor);
                if (Mathf.Abs(local.x) > 0.5f || Mathf.Abs(local.y) > 0.5f || Mathf.Abs(local.z) > 0.5f) continue;
                if (Covered(floor, probes) && Covered(floor + Vector3.up * 1.65f, probes)) continue;
                probes.Add(new Sphere { center = Common.ConvertVector(floor + Vector3.up * 1.5f), radius = batch.horizontalSpacing });
            }
            if (probes.Count == before)
            {
                Debug.Log("Probe coverage already includes all sampled walkable floor/head positions.");
                return;
            }
            Undo.RecordObject(batch, "Fill probe coverage gaps");
            var context = new Context();
            var native = new ProbeBatch(context);
            try
            {
                foreach (Sphere probe in probes) native.AddProbe(probe);
                native.Commit();
                batch.probeDataSize = native.Save(batch.asset);
            }
            finally { native.Release(); context.Release(); }
            array.arraySize = probes.Count;
            for (int i = 0; i < probes.Count; i++)
            {
                var p = array.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("center.x").floatValue = probes[i].center.x;
                p.FindPropertyRelative("center.y").floatValue = probes[i].center.y;
                p.FindPropertyRelative("center.z").floatValue = probes[i].center.z;
                p.FindPropertyRelative("radius").floatValue = probes[i].radius;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            batch.ResetLayers();
            EditorUtility.SetDirty(batch);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(batch.asset), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            EditorSceneManager.MarkSceneDirty(batch.gameObject.scene);
            Debug.Log($"Added {probes.Count - before} walkable-area probes; {probes.Count} total. Bake pathing again, then save the scene.");
        }

        private static bool Covered(Vector3 position, List<Sphere> probes)
        {
            for (int i = 0; i < probes.Count; i++)
                if ((position - Common.ConvertVector(probes[i].center)).sqrMagnitude < probes[i].radius * probes[i].radius) return true;
            return false;
        }
    }
}
#endif
