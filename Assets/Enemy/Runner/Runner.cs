namespace Enemy.Runner
{
    public class Runner : Enemy
    {
        protected void Start()
        {
            entityHealth.OnValueChanged += OnDamaged;
        }

        private void OnDamaged(float oldV, float newV)
        {
            if (newV <= 0 || newV >= oldV) return;
            Run();
        }

    }
}
