using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Rendering.Editor
{
    public sealed class GameplayShaderWarmupAuthoring : IPreprocessBuildWithReport
    {
        private const string CollectionPath = "Assets/Resources/GameplayShaderVariants.shadervariants";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            Rebuild();
        }

        [MenuItem("Tools/Rendering/Rebuild Gameplay Shader Variants")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebuild shader variants outside Play Mode.");

            var generated = new ShaderVariantCollection { name = "GameplayShaderVariants" };
            try
            {
                Add(generated, "Assets/Player/Equipment/Subwoofer/SubWooferZone.shader", PassType.ScriptableRenderPipeline);
                Add(generated, "Assets/Player/Equipment/Subwoofer/SubWooferZone 1.shader", PassType.ScriptableRenderPipeline);
                Add(generated, "Assets/Enemy/Shocker/ShockerEnergyField.shader", PassType.ScriptableRenderPipeline);

                AddOrbitalVariants(generated, "Assets/Base/Radar/Shaders/FieryOrbitalBeam.shader");
                AddOrbitalVariants(generated, "Assets/Base/Radar/Shaders/MoltenShockwave.shader");
                AddOrbitalVariants(generated, "Assets/Base/Radar/Shaders/MoltenCraterDecal.shader");

                // Untagged passes use Normal even when the shader runs in URP.
                Add(generated, "Assets/Resources/CaptureImplosion.shader", PassType.Normal);
                Add(generated, "Assets/Resources/CaptureParticles.shader", PassType.Normal);
                Add(generated, "Assets/Resources/CaptureParticles.shader", PassType.Normal, "INSTANCING_ON");
                Add(generated, "Assets/Base/Portals/CRTPortalScreen.shader", PassType.Normal);
                Add(generated, "Assets/Base/Portals/PortalDistortionShader.shader", PassType.Normal);
                Add(generated, "Assets/Base/SellPoint/CRTShutdownScreen.shader", PassType.Normal);
                Add(generated, "Assets/Shaders/ScreenShatter.shader", PassType.Normal);
                Add(generated, "Assets/Shaders/PainterlyFullscreen.shader", PassType.Normal);

                const string fogPath = "Assets/Shaders/VolumetricFox.shader";
                Add(generated, fogPath, PassType.Normal);
                Add(generated, fogPath, PassType.Normal, "_MAIN_LIGHT_SHADOWS");
                Add(generated, fogPath, PassType.Normal, "_MAIN_LIGHT_SHADOWS_CASCADE");
                Add(generated, fogPath, PassType.Normal, "_MAIN_LIGHT_SHADOWS_SCREEN");

                var existing = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(CollectionPath);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(generated, CollectionPath);
                }
                else
                {
                    EditorUtility.CopySerialized(generated, existing);
                    EditorUtility.SetDirty(existing);
                }

                AssetDatabase.SaveAssetIfDirty(existing != null ? existing : generated);
                Debug.Log($"[GameplayShaderWarmup] Saved {generated.variantCount} variants for {generated.shaderCount} shaders.");
            }
            finally
            {
                if (!AssetDatabase.Contains(generated))
                    UnityEngine.Object.DestroyImmediate(generated);
            }
        }

        private static void AddOrbitalVariants(ShaderVariantCollection collection, string path)
        {
            // Cover the declared fog and instancing modes across missions and quality settings.
            foreach (string fogKeyword in new[] { null, "FOG_LINEAR", "FOG_EXP", "FOG_EXP2" })
            {
                string[] keywords = fogKeyword == null ? Array.Empty<string>() : new[] { fogKeyword };
                Add(collection, path, PassType.ScriptableRenderPipeline, keywords);
                string[] instancedKeywords = fogKeyword == null
                    ? new[] { "INSTANCING_ON" }
                    : new[] { fogKeyword, "INSTANCING_ON" };
                Add(collection, path, PassType.ScriptableRenderPipeline, instancedKeywords);
            }
        }

        private static void Add(ShaderVariantCollection collection, string path, PassType passType, params string[] keywords)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new BuildFailedException($"Cannot warm up missing or broken shader: {path}");

            try
            {
                collection.Add(new ShaderVariantCollection.ShaderVariant(shader, passType, keywords));
            }
            catch (ArgumentException exception)
            {
                throw new BuildFailedException($"Invalid warmup variant for {path}, {passType}, [{string.Join(", ", keywords)}]: {exception.Message}");
            }
        }
    }
}
