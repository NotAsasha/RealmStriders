using NUnit.Framework.Internal.Filters;
using UnityEngine;
using UnityEngine.Rendering;

public class Runner : Enemy
{
    protected override void Start()
    {
        base.Start();
        entityHealth.OnValueChanged += OnDamaged;
    }

    private void OnDamaged(float oldV, float newV)
    {
        if (newV <= 0 || newV >= oldV) return;
        Run();
    }

}
