using UnityEngine;

namespace Enemy.Runner
{
    public class Runner : Enemy
    {
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) entityHealth.OnValueChanged += OnDamaged;
        }

        public override void OnNetworkDespawn()
        {
            entityHealth.OnValueChanged -= OnDamaged;
            base.OnNetworkDespawn();
        }

        private void OnDamaged(float oldValue, float newValue)
        {
            if (newValue <= 0 || newValue >= oldValue) return;
            Run();
        }

        public override void Lure(Vector3 coords)
        {
            Run(coords);
        }
    }
}
