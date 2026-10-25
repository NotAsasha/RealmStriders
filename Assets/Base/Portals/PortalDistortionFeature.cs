using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Portals
{
    /// <summary>
    /// URP Render Feature that applies a fullscreen portal-distortion pass
    /// whenever <see cref="_PortalDistortionIntensity"/> (global shader float) is > 0.
    ///
    /// Setup:
    ///   1. Add this feature to your URP Renderer asset (UniversalRenderer).
    ///   2. Assign the PortalDistortionShader material in the Inspector.
    ///   3. Add <see cref="PortalTransitionEffect"/> to a scene GameObject — it drives
    ///      the global intensity value automatically.
    /// </summary>
    public class PortalDistortionFeature : ScriptableRendererFeature
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [SerializeField] private Material material;

        // ── Runtime ───────────────────────────────────────────────────────────
        private DistortionPass _pass;

        // Global shader property IDs — set from PortalTransitionEffect each frame
        public static readonly int IntensityId = Shader.PropertyToID("_PortalDistortionIntensity");
        public static readonly int TimeId      = Shader.PropertyToID("_PortalDistortionTime");

        // ─────────────────────────────────────────────────────────────────────
        //  ScriptableRendererFeature
        // ─────────────────────────────────────────────────────────────────────

        public override void Create()
        {
            if (material == null) return;

            _pass = new DistortionPass(material)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || material == null) return;

            // Skip when effect is invisible — saves the full blit cost
            if (Shader.GetGlobalFloat(IntensityId) <= 0.0005f) return;

            renderer.EnqueuePass(_pass);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Inner render pass (RenderGraph API — Unity 6 / URP 17+)
        // ─────────────────────────────────────────────────────────────────────

        private class DistortionPass : ScriptableRenderPass
        {
            private readonly Material _mat;

            private class PassData
            {
                public Material material;
                public TextureHandle sourceTex;
            }

            public DistortionPass(Material mat)
            {
                _mat = mat;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mat == null) return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                // Only affect the active game camera (do not distort offscreen cameras / portal rendertextures)
                if (cameraData.cameraType != CameraType.Game) return;
                if (cameraData.camera != null && cameraData.camera.targetTexture != null) return;

                float intensity = Shader.GetGlobalFloat(IntensityId);
                if (intensity <= 0.0005f) return;

                float time = Shader.GetGlobalFloat(TimeId);
                _mat.SetFloat(IntensityId, intensity);
                _mat.SetFloat(TimeId, time);

                TextureHandle source = resourceData.activeColorTexture;

                TextureDesc desc = source.GetDescriptor(renderGraph);
                desc.depthBufferBits = 0;
                desc.clearBuffer     = false;
                TextureHandle temp = renderGraph.CreateTexture(desc);

                // Pass 1 — distort into temp
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Portal Distortion Blit", out var passData))
                {
                    passData.material  = _mat;
                    passData.sourceTex = source;

                    builder.UseTexture(passData.sourceTex, AccessFlags.Read);
                    builder.SetRenderAttachment(temp, 0);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.sourceTex, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                // Pass 2 — copy temp back to screen
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Portal Distortion Copy", out var passData))
                {
                    passData.sourceTex = temp;

                    builder.UseTexture(passData.sourceTex, AccessFlags.Read);
                    builder.SetRenderAttachment(source, 0);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.sourceTex, new Vector4(1, 1, 0, 0), 0f, false);
                    });
                }
            }
        }
    }
}
