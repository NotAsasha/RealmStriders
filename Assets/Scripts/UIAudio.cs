using UnityEngine;

namespace Game.UI
{
    public sealed class UIAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource hoverSource;
        [SerializeField] private AudioSource clickSource;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.18f;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.4f;
        private static UIAudio instance;
        private float nextHoverTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            // Unity does not serialize ignoreListenerPause on AudioSource.
            hoverSource.ignoreListenerPause = true;
            clickSource.ignoreListenerPause = true;
            DontDestroyOnLoad(gameObject);
        }

        private static UIAudio GetInstance()
        {
            if (instance == null)
                instance = Instantiate(Resources.Load<UIAudio>("UIAudio"));
            return instance;
        }

        public static void PlayHover()
        {
            UIAudio audio = GetInstance();
            if (Time.unscaledTime < audio.nextHoverTime) return;
            audio.nextHoverTime = Time.unscaledTime + 0.065f;
            audio.hoverSource.PlayOneShot(audio.hoverClip, audio.hoverVolume);
        }

        public static void PlayClick()
        {
            UIAudio audio = GetInstance();
            audio.hoverSource.Stop();
            audio.nextHoverTime = Time.unscaledTime + 0.065f;
            audio.clickSource.PlayOneShot(audio.clickClip, audio.clickVolume);
        }
    }
}
