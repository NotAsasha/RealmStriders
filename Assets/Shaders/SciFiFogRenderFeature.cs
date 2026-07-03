using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class VolumetricFogRenderFeature : ScriptableRendererFeature
{
    class FogPass : ScriptableRenderPass
    {
        private Material fogMaterial;
        private SciFiFogVolume volumeSettings;

        // Контейнер для передачі даних у графічний потік
        private class PassData
        {
            public Material material;
            public TextureHandle sourceTex;
        }

        public FogPass(Material material)
        {
            fogMaterial = material;
            // Критично важливо для пост-процесингу: вказуємо URP, що потрібен доступ до кольору
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (fogMaterial == null) return;

            var stack = VolumeManager.instance.stack;
            volumeSettings = stack.GetComponent<SciFiFogVolume>();
            if (volumeSettings == null || !volumeSettings.IsActive()) return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle source = resourceData.activeColorTexture;
            // 1. Отримуємо дескриптор текстури у новому форматі TextureDesc
            TextureDesc desc = resourceData.activeColorTexture.GetDescriptor(renderGraph);
            desc.depthBufferBits = 0; // Нам потрібен лише колір
            desc.clearBuffer = false; // Вимикаємо очищення, щоб зекономити ресурси

            TextureHandle tempTexture = renderGraph.CreateTexture(desc);

            // Передаємо параметри Volume у шейдер
            fogMaterial.SetColor("_Color", volumeSettings.fogColor.value);
            fogMaterial.SetFloat("_MaxDistance", volumeSettings.maxDistance.value);
            fogMaterial.SetFloat("_DensityMultiplier", volumeSettings.densityMultiplier.value);

            // ==========================================
            // ПРОХІД 1: Малюємо туман у тимчасову текстуру
            // ==========================================
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Volumetric Fog Render", out var passData))
            {
                passData.material = fogMaterial;

                // ВИПРАВЛЕННЯ: Призначаємо текстуру напряму
                passData.sourceTex = source;
                // Реєструємо, що цей прохід буде ЧИТАТИ з камери
                builder.UseTexture(passData.sourceTex, AccessFlags.Read);

                // Вказуємо, що ПИШЕМО у тимчасову текстуру
                builder.SetRenderAttachment(tempTexture, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.sourceTex, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // ==========================================
            // ПРОХІД 2: Копіюємо результат назад на екран
            // ==========================================
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Volumetric Fog Copy Back", out var passData))
            {
                // ВИПРАВЛЕННЯ: Призначаємо тимчасову текстуру
                passData.sourceTex = tempTexture;
                // Реєструємо, що цей прохід буде ЧИТАТИ з тимчасової текстури
                builder.UseTexture(passData.sourceTex, AccessFlags.Read);

                // І пишемо назад у фінальний колір камери
                builder.SetRenderAttachment(source, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Просте копіювання (Blit) без матеріалу
                    Blitter.BlitTexture(context.cmd, data.sourceTex, new Vector4(1, 1, 0, 0), 0.0f, false);
                });
            }
        }
    }

    [SerializeField] private Material material;
    private FogPass fogPass;

    public override void Create()
    {
        if (material == null) return;
        fogPass = new FogPass(material)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material != null)
        {
            renderer.EnqueuePass(fogPass);
        }
    }
}