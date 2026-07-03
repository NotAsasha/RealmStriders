using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
// 1. Задаємо красиву назву та шлях у меню Add Override
[VolumeComponentMenu("Custom/Broken Fog")]
// 2. Явно вказуємо, що компонент підтримується виключно в URP
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public class SciFiFogVolume : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Чи активний цей ефект?")]
    public BoolParameter isActive = new BoolParameter(false);

    [Tooltip("Колір туману")]
    public ColorParameter fogColor = new ColorParameter(Color.white);

    [Tooltip("Максимальна дистанція")]
    public ClampedFloatParameter maxDistance = new ClampedFloatParameter(100f, 0f, 500f);

    [Tooltip("Множник густини")]
    public ClampedFloatParameter densityMultiplier = new ClampedFloatParameter(1f, 0f, 10f);

    public bool IsActive() => isActive.value && densityMultiplier.value > 0f;

    public bool IsTileCompatible() => false;
}