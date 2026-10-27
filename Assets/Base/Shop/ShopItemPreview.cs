using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Base.Shop
{
    public sealed class ShopItemPreview : MonoBehaviour
    {
        [SerializeField] private Canvas shopCanvas;
        [SerializeField, Min(1f)] private float degreesPerSecond = 22f;
        private Scene previewScene;
        private GameObject rig;
        private Transform pivot;
        private Camera previewCamera;
        private RenderTexture texture;
        private RawImage output;
        private GameObject model;
        private float elapsed;

        public void Show(GameObject meshPrefab, RawImage image)
        {
            if (meshPrefab == null || image == null) return;
            Hide(output);
            EnsureRig();
            model = Instantiate(meshPrefab, pivot, false);
            pivot.localRotation = Quaternion.Euler(0f, -25f, 0f);
            output = image;
            output.texture = texture;
            output.color = new Color(1f, 1f, 1f, 0f);
            elapsed = 0f;
        }

        public void Hide(RawImage image)
        {
            if (image != output) return;
            if (output != null) output.texture = null;
            output = null;
            if (model != null) { model.SetActive(false); Destroy(model); }
            model = null;
            if (previewCamera != null) previewCamera.enabled = false;
        }

        private void EnsureRig()
        {
            if (rig != null) return;
            // Mesh-only assets keep previews independent of item physics and NGO.
            previewScene = SceneManager.CreateScene("Shop preview " + GetInstanceID());
            rig = new GameObject("Shop preview rig");
            SceneManager.MoveGameObjectToScene(rig, previewScene);
            rig.transform.position = new Vector3(0f, -10000f, 0f);
            pivot = new GameObject("Turntable").transform;
            pivot.SetParent(rig.transform, false);
            var cameraObject = new GameObject("Preview camera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, .25f, -4f);
            cameraObject.transform.LookAt(pivot);
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.scene = previewScene;
            previewCamera.cullingMask = 1 << 31;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.035f, .035f, .043f, 1f);
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 1.35f;
            previewCamera.nearClipPlane = .1f;
            previewCamera.farClipPlane = 10f;
            previewCamera.allowHDR = false;
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.volumeLayerMask = 0;
            texture = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32) { name = "Shop item preview" };
            texture.Create();
            previewCamera.targetTexture = texture;
            AddLight(new Vector3(35f, -30f, 0f), 1.8f);
            AddLight(new Vector3(20f, 140f, 0f), 1.2f);
        }

        private void AddLight(Vector3 rotation, float intensity)
        {
            var source = new GameObject("Preview light", typeof(Light));
            source.transform.SetParent(rig.transform, false);
            source.transform.localRotation = Quaternion.Euler(rotation);
            var light = source.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.cullingMask = 1 << 31;
            light.shadows = LightShadows.None;
        }

        private void LateUpdate()
        {
            if (previewCamera == null) return;
            bool visible = output != null && output.isActiveAndEnabled && shopCanvas.worldCamera != null;
            previewCamera.enabled = visible;
            if (!visible) return;
            pivot.Rotate(Vector3.up, degreesPerSecond * Time.unscaledDeltaTime, Space.Self);
            elapsed += Time.unscaledDeltaTime;
            output.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, elapsed / .3f));
        }

        private void OnDisable() => Hide(output);

        private void OnDestroy()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (previewScene.IsValid() && previewScene.isLoaded) SceneManager.UnloadSceneAsync(previewScene);
        }
    }
}
