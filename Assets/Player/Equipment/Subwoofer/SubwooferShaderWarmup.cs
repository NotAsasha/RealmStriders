using UnityEngine;
using UnityEngine.Rendering;

namespace Player.Equipment.Subwoofer
{
    /// <summary>
    /// Прогріває шейдери Subwoofer при старті гри, щоб уникнути
    /// фрізу GPU під час першого вмикання ефекту.
    /// </summary>
    internal static class SubwooferShaderWarmup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WarmUpShaders()
        {
            var collection = new ShaderVariantCollection();

            // Custom/SubWooferAcousticShockwave — основний шейдер деформації простору
            var shockwave = Shader.Find("Custom/SubWooferAcousticShockwave");
            if (shockwave != null)
            {
                collection.Add(new ShaderVariantCollection.ShaderVariant(
                    shockwave,
                    PassType.ForwardBase
                ));
            }
            else
            {
                Debug.LogWarning("[SubwooferShaderWarmup] Shader 'Custom/SubWooferAcousticShockwave' not found.");
            }

            // Custom/SubWooferZone — шейдер з Френелем та хвилями
            var zone = Shader.Find("Custom/SubWooferZone");
            if (zone != null)
            {
                collection.Add(new ShaderVariantCollection.ShaderVariant(
                    zone,
                    PassType.ForwardBase
                ));
            }
            else
            {
                Debug.LogWarning("[SubwooferShaderWarmup] Shader 'Custom/SubWooferZone' not found.");
            }

            if (!collection.isWarmedUp)
                collection.WarmUp();
        }
    }
}
