using System;
using FileSystem.Scripts;
using UnityEngine;

namespace Game.Rendering
{
    [DisallowMultipleComponent]
    public sealed class TerrainGraphicsQuality : MonoBehaviour
    {
        [SerializeField] private Terrain targetTerrain;
        [Tooltip("Matches the Quality Settings order: Performant, Balanced, High Fidelity.")]
        [SerializeField] private DetailQuality[] qualityLevels =
        {
            new(0.3f, 0.5f),
            new(0.65f, 0.75f),
            new(1f, 1f)
        };

        private float authoredDensity;
        private float authoredDistance;
        private bool hasBaseline;

        [Serializable]
        private struct DetailQuality
        {
            [Range(0f, 1f)] public float densityMultiplier;
            [Range(0f, 1f)] public float distanceMultiplier;

            public DetailQuality(float densityMultiplier, float distanceMultiplier)
            {
                this.densityMultiplier = densityMultiplier;
                this.distanceMultiplier = distanceMultiplier;
            }
        }

        private void OnEnable()
        {
            if (targetTerrain == null)
            {
                Debug.LogError("Assign a Terrain to TerrainGraphicsQuality.", this);
                return;
            }

            authoredDensity = targetTerrain.detailObjectDensity;
            authoredDistance = targetTerrain.detailObjectDistance;
            hasBaseline = true;
            SettingsFile.OnGraphicsSettingsApplied += ApplyQuality;
            ApplyQuality();
        }

        private void OnDisable()
        {
            SettingsFile.OnGraphicsSettingsApplied -= ApplyQuality;
            if (!hasBaseline) return;

            if (targetTerrain != null)
                SetDetails(authoredDensity, authoredDistance);
            hasBaseline = false;
        }

        private void ApplyQuality()
        {
            if (targetTerrain == null || qualityLevels == null || qualityLevels.Length == 0) return;

            int qualityIndex = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, qualityLevels.Length - 1);
            DetailQuality quality = qualityLevels[qualityIndex];
            // Scale the authored values, never the previous quality level's result.
            SetDetails(authoredDensity * Mathf.Clamp01(quality.densityMultiplier),
                authoredDistance * Mathf.Clamp01(quality.distanceMultiplier));
        }

        private void SetDetails(float density, float distance)
        {
            if (!Mathf.Approximately(targetTerrain.detailObjectDensity, density))
                targetTerrain.detailObjectDensity = density;
            if (!Mathf.Approximately(targetTerrain.detailObjectDistance, distance))
                targetTerrain.detailObjectDistance = distance;
        }
    }
}
