using System;
using System.Collections.Generic;
using SteamAudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vector3 = UnityEngine.Vector3;
using Scene = UnityEngine.SceneManagement.Scene;

namespace Game.Audio
{
    [DisallowMultipleComponent]
    public sealed class SceneAcoustics : MonoBehaviour
    {
        [SerializeField] private SteamAudioProbeBatch probeBatch;
        private static readonly Dictionary<int, SceneAcoustics> Environments = new Dictionary<int, SceneAcoustics>();
        public static event Action Changed;
        public SteamAudioProbeBatch ProbeBatch => probeBatch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            Environments.Clear();
            Changed = null;
        }

        private void OnEnable()
        {
            if (probeBatch == null) return;
            Environments[gameObject.scene.handle] = this;
            Changed?.Invoke();
        }

        private void OnDisable()
        {
            if (Environments.TryGetValue(gameObject.scene.handle, out SceneAcoustics current) && current == this)
            {
                Environments.Remove(gameObject.scene.handle);
                Changed?.Invoke();
            }
        }

        public static bool TryGet(Scene scene, out SceneAcoustics environment)
        {
            return Environments.TryGetValue(scene.handle, out environment);
        }

        public static bool TryGetAtPosition(Scene preferredScene, Vector3 position, out SceneAcoustics environment)
        {
            if (TryGet(preferredScene, out environment) && environment.Contains(position)) return true;
            // Persistent players can enter an additive mission without changing their owning scene.
            foreach (SceneAcoustics candidate in Environments.Values)
            {
                if (candidate == null || !candidate.Contains(position)) continue;
                environment = candidate;
                return true;
            }
            environment = null;
            return false;
        }

        public bool Contains(Vector3 position)
        {
            Vector3 local = probeBatch.transform.InverseTransformPoint(position);
            return Mathf.Abs(local.x) <= 0.5f && Mathf.Abs(local.y) <= 0.5f && Mathf.Abs(local.z) <= 0.5f;
        }
    }
}
