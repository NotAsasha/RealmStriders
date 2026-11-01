using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering.Universal;

public static class CrouchCameraChecks
{
    public static string Configure()
    {
        const string path = "Assets/Player/Player.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var camera = root.GetComponentInChildren<Player.Movement.CameraMovement>(true);
            var so = new SerializedObject(camera);
            so.FindProperty("crouchCameraForward").floatValue = .4f;
            var renderers = so.FindProperty("firstPersonHiddenRenderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = root.transform.Find("Body/SpacemanBody/SpaceManHead").GetComponent<SkinnedMeshRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return "Configured crouch camera offset and explicit owner helmet renderer.";
    }

    public static string Preview(float forward)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(960, 540, 24);
        var previous = RenderTexture.active;
        Camera camera = null;
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab"), scene);
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            foreach (var rig in root.GetComponentsInChildren<RigBuilder>(true)) rig.enabled = false;
            var animator = root.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false; animator.Rebind(); animator.Update(0);
            animator.SetBool("Crouched", true);
            animator.SetFloat("CrouchMoveSpeed", 1);
            camera = root.GetComponentInChildren<Camera>(true);
            var cameraMovement = root.GetComponentInChildren<Player.Movement.CameraMovement>(true);
            var hiddenHead = root.transform.Find("Body/SpacemanBody/SpaceManHead").GetComponent<Renderer>();
            var body = root.transform.Find("Body/SpacemanBody").GetComponent<Renderer>();
            var originalHeadMode = hiddenHead.shadowCastingMode;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var visibility = typeof(Player.Movement.CameraMovement).GetMethod("SetFirstPersonVisibility", flags);
            if (forward > 0)
            {
                typeof(Player.Movement.CameraMovement).GetMethod("CacheFirstPersonVisibility", flags).Invoke(cameraMovement, null);
                visibility.Invoke(cameraMovement, new object[] { true });
                if (hiddenHead.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly ||
                    body.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.On)
                    throw new InvalidOperationException("Head-only visibility or body shadow changed unexpectedly");
            }
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.25f, .35f, .45f);
            camera.targetTexture = target;
            camera.aspect = 16f / 9f;
            var light = new GameObject("CameraCheckLight", typeof(Light));
            SceneManager.MoveGameObjectToScene(light, scene);
            light.GetComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 2;
            light.transform.rotation = Quaternion.Euler(30, -140, 0);
            foreach (string state in new[] { "Crouch Idle", "Crouch Walk" })
            {
                animator.SetBool("Moving", state == "Crouch Walk");
                animator.Play(state, 0, .4f); animator.Update(0);
                foreach (float pitch in new[] { 0f, 60f, -60f })
                {
                    animator.Play(state, 0, .4f); animator.Update(0);
                    var headSync = root.GetComponentInChildren<PlayerHeadSync>(true);
                    float bonePitch = Mathf.Clamp(pitch, headSync.MinPitchAngle, headSync.MaxPitchAngle);
                    headSync.spineBone.localRotation *= Quaternion.Euler(bonePitch * headSync.spinePitchFraction, 0, 0);
                    headSync.headBone.localRotation *= Quaternion.Euler(bonePitch * (1 - headSync.spinePitchFraction), 0, 0);
                    camera.transform.position = new Vector3(0, .05f, forward);
                    camera.transform.rotation = Quaternion.Euler(pitch, 0, 0);
                    camera.Render(); RenderTexture.active = target;
                    var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                    File.WriteAllBytes("tools/camera-" + (forward == 0 ? "before" : "after") + "-" + state.Replace(" ", "-") + "-" + pitch + ".png", pixels.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(pixels);
                }
            }
            if (forward > 0)
            {
                visibility.Invoke(cameraMovement, new object[] { false });
                if (hiddenHead.shadowCastingMode != originalHeadMode)
                    throw new InvalidOperationException("External view did not restore the helmet");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab");
                if (source.transform.Find("Body/SpacemanBody/SpaceManHead").GetComponent<Renderer>().shadowCastingMode != originalHeadMode)
                    throw new InvalidOperationException("Local visibility changed the shared prefab");
            }
            return "Captured idle/walk at forward, down and up look angles.";
        }
        finally
        {
            if (camera != null) camera.targetTexture = null;
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
