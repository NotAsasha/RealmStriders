#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using Object = UnityEngine.Object;

namespace Game.Worlds.Editor
{
    public static class FieldEnvironmentAuthoring
    {
        private const string PrefabPath = "Assets/Enviroment/FieldEnvironment.prefab";
        private const string MeshPath = "Assets/Enviroment/FieldEnvironmentMeshes.asset";

        [MenuItem("Tools/Worlds/Refresh Shared Field Meshes")]
        public static void RefreshMeshes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Stop Play Mode, lighting baking and building before refreshing Field meshes.");

            GameObject source = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (ProBuilderMesh mesh in source.GetComponentsInChildren<ProBuilderMesh>(true))
                    UnityEditor.ProBuilder.EditorUtility.SynchronizeWithMeshFilter(mesh);

                if (AssetDatabase.LoadMainAssetAtPath(MeshPath) == null)
                {
                    var index = new TextAsset("Shared Field environment meshes. Edit FieldEnvironment.prefab and refresh shared meshes.")
                    {
                        name = "FieldEnvironmentMeshes"
                    };
                    AssetDatabase.CreateAsset(index, MeshPath);
                }

                var existing = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>()
                    .ToDictionary(mesh => mesh.name, StringComparer.Ordinal);
                foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    string path = AnimationUtility.CalculateTransformPath(filter.transform, source.transform);
                    string name = path.Replace('/', '_');
                    if (filter.sharedMesh == null)
                        throw new InvalidOperationException($"Missing source mesh at {path}.");

                    if (!existing.TryGetValue(name, out Mesh shared))
                    {
                        shared = Object.Instantiate(filter.sharedMesh);
                        shared.name = name;
                        shared.hideFlags = HideFlags.None;
                        AssetDatabase.AddObjectToAsset(shared, MeshPath);
                        existing.Add(name, shared);
                    }
                    else
                    {
                        // Keep subasset IDs stable so both scenes retain their mesh references.
                        EditorUtility.CopySerialized(filter.sharedMesh, shared);
                        shared.name = name;
                        shared.hideFlags = HideFlags.None;
                        EditorUtility.SetDirty(shared);
                    }
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
            Debug.Log("Updated shared Field meshes. Rebuild lighting and navigation after geometry changes.");
        }
    }
}
#endif
