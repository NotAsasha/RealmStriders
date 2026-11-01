using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using Player.Movement;
using Assets.Player.UI;
using FileSystem.Scripts;
using TMPro;

public static class CrouchPoseChecks
{
    public static string Measure()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab"), scene);
            var animator = root.GetComponent<Animator>();
            foreach (var rig in root.GetComponentsInChildren<RigBuilder>(true)) rig.enabled = false;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0);
            string result = "";
            foreach (string state in new[] { "Crouch Idle", "Crouch Walk" })
            {
                animator.SetBool("Crouched", true); animator.SetBool("Moving", state == "Crouch Walk");
                var headBounds = new Bounds(); var hipBounds = new Bounds();
                float minFoot = float.MaxValue, maxFoot = float.MinValue;
                for (int i = 0; i <= 60; i++)
                {
                    animator.Play(state, 0, i / 60f); animator.Update(0);
                    Vector3 head = animator.GetBoneTransform(HumanBodyBones.Head).position;
                    Vector3 hip = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    float foot = Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,
                        animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                    minFoot = Mathf.Min(minFoot, foot); maxFoot = Mathf.Max(maxFoot, foot);
                    if (i == 0) { headBounds = new Bounds(head, Vector3.zero); hipBounds = new Bounds(hip, Vector3.zero); }
                    else { headBounds.Encapsulate(head); hipBounds.Encapsulate(hip); }
                }
                result += state + " head=" + headBounds + " hips=" + hipBounds + " foot="+minFoot+".."+maxFoot+"\n";
            }
            return result;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit Mode only");
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(800, 800, 24);
        var previous = RenderTexture.active;
        Camera camera = null;
        Controls controls = null;
        SettingsFile settings = null;
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab"), scene);
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            foreach (var rig in root.GetComponentsInChildren<RigBuilder>(true)) rig.enabled = false;
            var animator = root.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
            foreach (var child in controller.layers[0].stateMachine.states)
                if (child.state.speedParameterActive)
                    Require(controller.parameters.Any(p => p.name == child.state.speedParameter), "Invalid state speed parameter");
            animator.Rebind(); animator.Update(0);
            var cam = new GameObject("Pose Preview", typeof(Camera)); SceneManager.MoveGameObjectToScene(cam, scene);
            camera = cam.GetComponent<Camera>();
            camera.transform.position = new Vector3(3, .8f, 4);
            camera.transform.LookAt(new Vector3(0, -.1f, 0));
            camera.fieldOfView = 35;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.targetTexture = target; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.17f,.18f,.2f);
            var light = new GameObject("Pose Light", typeof(Light)); SceneManager.MoveGameObjectToScene(light, scene);
            light.GetComponent<Light>().type = LightType.Directional; light.GetComponent<Light>().intensity = 2;
            light.transform.rotation = Quaternion.Euler(30, -140, 0);
            animator.SetFloat("StandingSpeed", 0);
            animator.SetFloat("CrouchMoveSpeed", 1);
            animator.Play("mixamo_com", 0, 0); animator.Update(0);
            float standingHead = animator.GetBoneTransform(HumanBodyBones.Head).position.y;
            Capture(camera, target, "standing");
            animator.SetBool("Crouched", true);
            animator.SetBool("Moving", false);
            for (int i = 0; i < 30; i++) animator.Update(1f / 60);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Crouch Idle"), "Stationary crouch transition failed");
            float crouchHead = animator.GetBoneTransform(HumanBodyBones.Head).position.y;
            Require(standingHead - crouchHead > .25f, "Humanoid crouch did not lower the body");
            float foot = Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,
                animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
            Capture(camera, target, "idle");
            animator.SetBool("Moving", true);
            for (int i = 0; i < 30; i++) animator.Update(1f / 60);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Crouch Walk"), "Crouch walk transition failed");
            Capture(camera, target, "walk");
            animator.SetBool("Crouched", false);
            for (int i = 0; i < 30; i++) animator.Update(1f / 60);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("mixamo_com"), "Stand transition failed");

            var menuRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/UI/Settings.prefab"), scene);
            var menu = menuRoot.GetComponent<SettingsMenu>();
            var so = new SerializedObject(menu);
            var dropdown = (TMP_Dropdown)so.FindProperty("crouchModeDropdown").objectReferenceValue;
            Require(dropdown != null && dropdown.options.Count == 2, "Mode options missing");
            Require(dropdown.onValueChanged.GetPersistentTarget(0) == menu && dropdown.onValueChanged.GetPersistentMethodName(0) == "UpdateCrouchMode", "Mode callback missing");
            settings = ScriptableObject.CreateInstance<SettingsFile>(); settings.save = new SettingsSave();
            typeof(SettingsMenu).GetField("settingsFile", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, settings);
            dropdown.onValueChanged.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
            dropdown.value = 1;
            Require(settings.save.crouchMode == CrouchMode.Toggle, "Toggle setting callback");
            dropdown.value = 0;
            Require(settings.save.crouchMode == CrouchMode.Hold, "Hold setting callback");
            var binding = menuRoot.transform.Find("Controls/BindButtons/Crouch").GetComponentInChildren<InputBindings>(true);
            var reference = (InputActionReference)new SerializedObject(binding).FindProperty("actionToBind").objectReferenceValue;
            Require(reference.action.name == "Crouch", "Crouch rebind reference");
            controls = new Controls();
            controls.Gameplay.Crouch.ApplyBindingOverride("<Keyboard>/c");
            string rebinds = controls.SaveBindingOverridesAsJson();
            var restored = new Controls();
            try
            {
                restored.LoadBindingOverridesFromJson(rebinds);
                Require(restored.Gameplay.Crouch.bindings[0].effectivePath == "<Keyboard>/c", "Crouch rebind restore");
            }
            finally { UnityEngine.Object.DestroyImmediate(restored.asset); }
            return $"PASS: real humanoid Animator stationary crouch/walk/stand transitions; head Y {standingHead:F3} -> {crouchHead:F3}, foot Y {foot:F3}; settings callback + input rebind round trip. Pose previews written.";
        }
        finally
        {
            if (controls != null) UnityEngine.Object.DestroyImmediate(controls.asset);
            if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
            if (camera != null) camera.targetTexture = null;
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
    private static void Capture(Camera camera, RenderTexture target, string name)
    {
        camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        File.WriteAllBytes("tools/crouch-" + name + ".png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
