using System;
using UnityEngine;

namespace Game.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class SurrealClockPresentation : MonoBehaviour
    {
        public enum Motion
        {
            Sweep,
            Stepped,
            Rewind
        }

        [Serializable]
        public struct Hand
        {
            public Transform pivot;
            public Vector3 localAxis;
            public Motion motion;
            public float degreesPerSecond;
            public float stepDegrees;
            [Min(0.01f)] public float stepInterval;
            [Min(0.01f)] public float stepTravelDuration;
            [Tooltip("Offset within this hand's motion cycle, in seconds.")]
            public float phase;
            [Min(0.01f)] public float reversalPeriod;
            [Min(0.01f)] public float reversalDuration;
            public float reversalDegrees;
        }

        [SerializeField] private Hand[] hands = Array.Empty<Hand>();

        private Quaternion[] restRotations;
        private double elapsed;

        private void Awake() => CaptureRestRotations();

        private void Update()
        {
            elapsed += Time.deltaTime;
            Sample(elapsed);
        }

        public void Sample(double seconds)
        {
            CaptureRestRotations();
            if (!IsFinite(seconds) || hands == null) return;

            int count = Math.Min(hands.Length, restRotations.Length);
            for (int i = 0; i < count; i++)
            {
                Hand hand = hands[i];
                if (hand.pivot == null || hand.localAxis.sqrMagnitude < 0.000001f) continue;
                hand.pivot.localRotation = restRotations[i] *
                    Quaternion.AngleAxis(EvaluateAngle(hand, seconds), hand.localAxis);
            }
        }

        public static float EvaluateAngle(Hand hand, double seconds)
        {
            if (!IsFinite(seconds)) return 0f;
            double phase = IsFinite(hand.phase) ? hand.phase : 0d;
            double angle = EvaluateUnwrapped(hand, seconds + phase) - EvaluateUnwrapped(hand, phase);
            return IsFinite(angle) ? (float)(angle % 360d) : 0f;
        }

        private static double EvaluateUnwrapped(Hand hand, double seconds)
        {
            if (hand.motion == Motion.Stepped)
            {
                double interval = PositiveDuration(hand.stepInterval);
                double cycle = Math.Floor(seconds / interval);
                double travel = Math.Min(interval, PositiveDuration(hand.stepTravelDuration));
                double progress = Math.Clamp((seconds - cycle * interval) / travel, 0d, 1d);
                double remaining = progress - 1d;
                // A small mechanical overshoot settles before the next tick.
                double eased = 1d + 1.65d * remaining * remaining * remaining +
                    0.65d * remaining * remaining;
                return (cycle + eased) * hand.stepDegrees;
            }

            double angle = seconds * hand.degreesPerSecond;
            if (hand.motion != Motion.Rewind) return angle;

            double period = PositiveDuration(hand.reversalPeriod);
            double duration = Math.Min(period, PositiveDuration(hand.reversalDuration));
            double cycleTime = seconds - Math.Floor(seconds / period) * period;
            if (cycleTime >= duration) return angle;

            // The excursion rejoins the sweep with zero displacement and slope.
            double envelope = Math.Sin(Math.PI * cycleTime / duration);
            return angle - hand.reversalDegrees * envelope * envelope;
        }

        private void CaptureRestRotations()
        {
            if (restRotations != null || hands == null) return;
            restRotations = new Quaternion[hands.Length];
            for (int i = 0; i < hands.Length; i++)
                restRotations[i] = hands[i].pivot != null ? hands[i].pivot.localRotation : Quaternion.identity;
        }

        private void OnDisable()
        {
            elapsed = 0d;
            if (hands == null || restRotations == null) return;
            int count = Math.Min(hands.Length, restRotations.Length);
            for (int i = 0; i < count; i++)
                if (hands[i].pivot != null) hands[i].pivot.localRotation = restRotations[i];
        }

        private static double PositiveDuration(float value) =>
            IsFinite(value) ? Math.Max(0.01d, value) : 0.01d;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
