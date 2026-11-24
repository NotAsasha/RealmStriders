using System.Collections;
using UnityEngine;

namespace Game.Rendering
{
    public sealed class EndSkyEffects : MonoBehaviour
    {
        [SerializeField] private Terrain terrain;
        [SerializeField] private Transform eye;
        [SerializeField] private Transform corona;
        [SerializeField] private ParticleSystem ash;
        [SerializeField] private LineRenderer wave;
        [SerializeField] private AudioSource eyeSound;

        private const float AshRate = 40f;
        private const int WaveSegments = 128;
        private readonly Vector3[] ring = new Vector3[WaveSegments];
        private readonly Vector2[] directions = new Vector2[WaveSegments];
        private readonly System.Random random = new System.Random(731);
        private Vector3 eyeScale;
        private Vector3 coronaScale;
        private float emission;
        private bool played;

        private void Awake()
        {
            eyeScale = eye.localScale;
            coronaScale = corona.localScale;
            wave.useWorldSpace = true;
            wave.loop = true;
            wave.positionCount = WaveSegments;
            wave.enabled = false;
            for (int i = 0; i < WaveSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / WaveSegments;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            ash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ash.main;
            main.loop = true;
            main.maxParticles = 900;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var automaticEmission = ash.emission;
            automaticEmission.enabled = false;
            var shape = ash.shape;
            shape.enabled = false;

            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(new Color(0.09f, 0.07f, 0.11f), 0f),
                new GradientColorKey(new Color(0.27f, 0.14f, 0.1f), 0.7f),
                new GradientColorKey(new Color(1f, 0.65f, 0.2f), 0.93f)
            }, new[] {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.04f),
                new GradientAlphaKey(1f, 0.94f), new GradientAlphaKey(0f, 1f)
            });
            var colors = ash.colorOverLifetime;
            colors.enabled = true;
            colors.color = gradient;
            ash.Play();
            // Populate the upper sky before the first player crosses the portal.
            for (int i = 0; i < 45; i++)
            {
                for (int j = 0; j < (int)AshRate; j++) EmitFlake();
                ash.Simulate(1f, false, false, false);
            }
            ash.Play();
        }

        private void OnEnable() => GameManager.FinalTargetsCaptured += PlayFinale;

        private void OnDisable()
        {
            GameManager.FinalTargetsCaptured -= PlayFinale;
            StopAllCoroutines();
            eyeSound.Stop();
            eye.localScale = eyeScale;
            corona.localScale = coronaScale;
            wave.enabled = false;
        }

        private void Update()
        {
            emission += Time.deltaTime * AshRate;
            while (emission >= 1f)
            {
                EmitFlake();
                emission -= 1f;
            }
        }

        private void EmitFlake()
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 start = origin + new Vector3((float)random.NextDouble() * size.x, 0f,
                (float)random.NextDouble() * size.z);
            start.y += terrain.SampleHeight(start) + 0.3f;
            Vector3 radial = start - eye.position;
            radial.y = 0f;
            Vector3 destination = eye.position + radial.normalized * (corona.lossyScale.y * 0.38f);
            float life = Mathf.Lerp(32f, 49f, (float)random.NextDouble());
            ash.Emit(new ParticleSystem.EmitParams
            {
                position = start, velocity = (destination - start) / life, startLifetime = life,
                startSize = Mathf.Lerp(0.12f, 0.65f, (float)random.NextDouble()),
                rotation = (float)random.NextDouble() * 180f, startColor = Color.white
            }, 1);
        }

        private void PlayFinale()
        {
            if (played) return;
            played = true;
            StartCoroutine(Blink());
        }

        private IEnumerator Blink()
        {
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                float open = t < 0.95f ? 1f - Mathf.SmoothStep(0f, 1f, t / 0.95f)
                    : Mathf.SmoothStep(0f, 1f, (t - 1.2f) / 0.8f);
                eye.localScale = Vector3.Scale(eyeScale, new Vector3(Mathf.Lerp(0.015f, 1f, open), 1f, 1f));
                corona.localScale = Vector3.Scale(coronaScale, new Vector3(open, 1f, 1f));
                yield return null;
            }
            eye.localScale = eyeScale;
            corona.localScale = coronaScale;

            while (true)
            {
                eyeSound.Play();
                yield return ExpandWave();
                while (eyeSound.isPlaying) yield return null;
            }
        }

        private IEnumerator ExpandWave()
        {
            Vector3 center = eye.position;
            Vector3 size = terrain.terrainData.size;
            float reach = new Vector2(size.x, size.z).magnitude * 0.5f;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                float progress = t / 3f;
                float radius = Mathf.Lerp(1f, reach, progress);
                for (int i = 0; i < ring.Length; i++)
                {
                    Vector2 offset = directions[i] * radius;
                    Vector3 point = new Vector3(center.x + offset.x, 0f, center.z + offset.y);
                    point.y = terrain.transform.position.y + terrain.SampleHeight(point) + 0.65f;
                    ring[i] = point;
                }
                float alpha = (1f - Mathf.SmoothStep(0f, 1f, progress)) * 0.85f;
                wave.startColor = wave.endColor = new Color(1f, 0.64f, 0.19f, alpha);
                wave.widthMultiplier = Mathf.Lerp(1.2f, 4f, progress);
                wave.SetPositions(ring);
                wave.enabled = true;
                yield return null;
            }
            wave.enabled = false;
        }
    }
}

