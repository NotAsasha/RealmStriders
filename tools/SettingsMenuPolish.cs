using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Player.UI;

public static class SettingsMenuPolish
{
    private const string Path = "Assets/Player/UI/Settings.prefab";
    private static readonly Color Surface = new Color32(49, 43, 56, 255);
    private static readonly Color Accent = new Color32(218, 177, 117, 255);
    private static readonly Color Foreground = new Color32(239, 231, 219, 255);

    private static Image Rule(Transform parent, string name, float inset, float bottom, float height)
    {
        var existing = parent.Find(name);
        var rect = existing != null ? (RectTransform)existing :
            new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(.5f, 0);
        rect.offsetMin = new Vector2(inset, bottom);
        rect.offsetMax = new Vector2(-inset, bottom + height);
        var image = rect.GetComponent<Image>();
        image.color = Accent;
        image.raycastTarget = false;
        return image;
    }

    private static void StyleSelectable(Selectable control)
    {
        var colors = control.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.2f, 1.15f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.7f, .65f, .6f);
        colors.disabledColor = new Color(.45f, .45f, .45f);
        colors.fadeDuration = .12f;
        control.transition = Selectable.Transition.ColorTint;
        control.colors = colors;
    }

    public static string Apply()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool reopen = stage != null && stage.assetPath == Path;
        if (reopen && stage.scene.isDirty) throw new InvalidOperationException("The Settings prefab has unsaved edits.");
        if (reopen) StageUtility.GoToMainStage();
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var menu = root.GetComponent<SettingsMenu>();
            var so = new SerializedObject(menu);
            var entries = so.FindProperty("categories");
            entries.arraySize = 3;
            string[] names = { "Graphics", "Sound", "Controls" };
            root.transform.Find("Background").GetComponent<Image>().color = new Color32(29, 26, 35, 255);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = Foreground;
                text.raycastTarget = false;
            }
            root.transform.Find("Title").GetComponent<TMP_Text>().color = Accent;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                button.image.sprite = null;
                button.image.color = Surface;
                StyleSelectable(button);
            }
            for (int i = 0; i < names.Length; i++)
            {
                var page = root.transform.Find(names[i]);
                page.GetComponent<Image>().color = new Color32(24, 21, 29, 150);
                var button = root.transform.Find("CategoriesButtons/" + names[i]).GetComponent<Button>();
                button.image.color = Color.white;
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 38;
                var indicator = Rule(button.transform, "ActiveIndicator", 22, 0, 3);
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("panel").objectReferenceValue = page.gameObject;
                entry.FindPropertyRelative("button").objectReferenceValue = button;
                entry.FindPropertyRelative("indicator").objectReferenceValue = indicator;
                for (int j = button.onClick.GetPersistentEventCount() - 1; j >= 0; j--)
                    if (button.onClick.GetPersistentTarget(j) == menu &&
                        button.onClick.GetPersistentMethodName(j) == "SelectCategory")
                        UnityEventTools.RemovePersistentListener(button.onClick, j);
                UnityEventTools.AddIntPersistentListener(button.onClick, menu.SelectCategory, i);
            }
            foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                dropdown.image.sprite = null;
                dropdown.image.color = Surface;
                StyleSelectable(dropdown);
                dropdown.captionText.color = Accent;
                dropdown.captionText.enableAutoSizing = true;
                dropdown.captionText.fontSizeMin = 26;
                dropdown.captionText.fontSizeMax = 44;
                foreach (var image in dropdown.template.GetComponentsInChildren<Image>(true))
                    image.color = image.name.Contains("Checkmark") ? Accent : Surface;
                foreach (var text in dropdown.template.GetComponentsInChildren<TMP_Text>(true))
                    text.color = Foreground;
                var arrow = dropdown.transform.Find("Arrow");
                if (arrow != null) arrow.GetComponent<Image>().color = Accent;
            }
            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
            {
                foreach (var image in slider.GetComponentsInChildren<Image>(true))
                {
                    image.sprite = null;
                    image.color = image.name == "Background" ? Surface : Accent;
                }
                StyleSelectable(slider);
            }
            var volume = (Slider)so.FindProperty("masterVolumeSlider").objectReferenceValue;
            volume.minValue = 0; volume.maxValue = 1;
            var toggle = (Toggle)so.FindProperty("vSyncToggle").objectReferenceValue;
            toggle.targetGraphic.color = Surface; toggle.graphic.color = Accent;
            StyleSelectable(toggle);
            root.transform.Find("Controls/Mouse Sensativity/Mouse Text").GetComponent<TMP_Text>().text = "Mouse Sensitivity";
            var apply = root.transform.Find("Footer/Apply").GetComponent<Button>();
            apply.image.color = new Color32(102, 77, 51, 255);
            Rule(root.transform.Find("Title"), "TitleRule", 80, -6, 2);
            so.ApplyModifiedPropertiesWithoutUndo();
            menu.SelectCategory(0);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            if (reopen) PrefabStageUtility.OpenPrefab(Path);
        }
        return "Category buttons wired; existing layout/fonts retained; restrained plum/bronze palette applied.";
    }

    public static string VerifyAndPreview()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(1280, 720, 24);
        var previous = RenderTexture.active;
        Camera camera = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var cameraObject = new GameObject("Settings preview", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.GetComponent<Camera>();
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            root.SetActive(true);
            var menu = root.GetComponent<SettingsMenu>();
            var serialized = new SerializedObject(menu);
            var categories = serialized.FindProperty("categories");
            if (categories.arraySize != 3) throw new Exception("Expected three categories.");
            string[] names = { "Graphics", "Sound", "Controls" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = root.transform.Find("CategoriesButtons/" + names[i]).GetComponent<Button>();
                if (button.onClick.GetPersistentEventCount() != 1 ||
                    button.onClick.GetPersistentTarget(0) != menu ||
                    button.onClick.GetPersistentMethodName(0) != "SelectCategory")
                    throw new Exception("Category callback missing or duplicated: " + names[i]);
                // Persistent RuntimeOnly listeners are disabled in Edit Mode; enable on this disposable clone.
                button.onClick.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
                button.onClick.Invoke();
                for (int j = 0; j < names.Length; j++)
                {
                    if (root.transform.Find(names[j]).gameObject.activeSelf != (i == j))
                        throw new Exception("Category click failed: " + names[i]);
                    var indicator = root.transform.Find("CategoriesButtons/" + names[j] + "/ActiveIndicator").GetComponent<Image>();
                    if (indicator.enabled != (i == j)) throw new Exception("Selection indicator failed.");
                }
                Canvas.ForceUpdateCanvases();
                foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes("tools/settings-refined-" + names[i].ToLowerInvariant() + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            menu.SelectCategory(-1);
            menu.SelectCategory(99);
            if (!root.transform.Find("Controls").gameObject.activeSelf) throw new Exception("Invalid index changed category.");
            typeof(SettingsMenu).GetMethod("OnEnable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(menu, null);
            if (!root.transform.Find("Graphics").gameObject.activeSelf) throw new Exception("Reopen did not select Graphics.");
            foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
                if (dropdown.onValueChanged.GetPersistentEventCount() == 0) throw new Exception("Missing setting callback.");
            return "PASS: real category button events, exclusive panels, selected indicators, invalid indices, reopen default, existing dropdown callbacks. Three previews captured.";
        }
        finally
        {
            if (camera != null) camera.targetTexture = null;
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
