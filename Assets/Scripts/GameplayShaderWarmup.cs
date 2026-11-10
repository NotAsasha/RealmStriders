using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Rendering
{
    internal static class GameplayShaderWarmup
    {
        // Retain the collection so its shaders survive scene unloads and UnloadUnusedAssets.
        private static ShaderVariantCollection variants;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            variants = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void WarmUpShaders()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;

            variants = Resources.Load<ShaderVariantCollection>("GameplayShaderVariants");
            if (variants == null)
            {
                Debug.LogError("[GameplayShaderWarmup] Missing GameplayShaderVariants. Run Tools/Rendering/Rebuild Gameplay Shader Variants.");
                return;
            }

            if (!variants.isWarmedUp)
                variants.WarmUp();
        }
    }
}
