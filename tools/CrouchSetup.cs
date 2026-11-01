using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Assets.Player.UI;
using Player;
using Player.Movement;

public static class CrouchSetup
{
    private const string InputPath = "Assets/Player/Movement/Controls.inputactions";
    private const string PlayerPath = "Assets/Player/Player.prefab";
    private const string SettingsPath = "Assets/Player/UI/Settings.prefab";

    public static string Input()
    {
        var asset = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath));
        try
        {
            asset.name = "Controls";
            var map = asset.FindActionMap("Gameplay", true);
            if (map.FindAction("Crouch") == null)
                map.AddAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl", expectedControlLayout: "Button");
            map.FindAction("Crouch", true).wantsInitialStateCheck = true;
            File.WriteAllText(InputPath, asset.ToJson());
            AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceUpdate);
        }
        finally { UnityEngine.Object.DestroyImmediate(asset); }
        return "Crouch action added; Input System importer regenerates Controls.cs.";
    }

    public static string Clips()
    {
        foreach (string name in new[] { "X Bot@Crouched Walking", "X Bot@Crouching Idle" })
        {
            string path = "Assets/Player/Animations/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clips = importer.clipAnimations;
            if (clips.Length == 0) clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.heightFromFeet = false;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
        return "Looping in-place humanoid clips configured.";
    }

    public static string Prefabs()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var movement = root.GetComponent<PlayerMovement>();
            var so = new SerializedObject(movement);
            so.FindProperty("playerCamera").objectReferenceValue = root.GetComponentInChildren<Camera>(true);
            so.FindProperty("human").objectReferenceValue = root.GetComponent<Human>();
            // The walking clip rises higher than the idle pose; cover both with the capsule.
            so.FindProperty("crouchHeight").floatValue = 1.6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var animation = root.GetComponentInChildren<PlayerAnimation>(true);
            so = new SerializedObject(animation);
            so.FindProperty("movement").objectReferenceValue = movement;
            so.FindProperty("human").objectReferenceValue = root.GetComponent<Human>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        root = PrefabUtility.LoadPrefabContents(SettingsPath);
        try
        {
            var menu = root.GetComponent<SettingsMenu>();
            var page = root.transform.Find("Controls");
            var grid = page.Find("BindButtons");
            Place(page.Find("Mouse Sensativity"), 136, 350, 1648, 110);
            Place(grid, 136, 480, 1648, 324);
            if (grid.Find("Crouch") == null)
            {
                var row = UnityEngine.Object.Instantiate(grid.Find("Run").gameObject, grid);
                row.name = "Crouch";
                var binding = row.GetComponentInChildren<InputBindings>(true);
                var so = new SerializedObject(binding);
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
                var action = asset.FindAction("Gameplay/Crouch", true);
                var reference = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>()
                    .First(r => r.action.id == action.id);
                so.FindProperty("actionToBind").objectReferenceValue = reference;
                var buttonText = (TMP_Text)so.FindProperty("buttonText").objectReferenceValue;
                buttonText.text = "Left Ctrl";
                foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
                    if (text != buttonText) text.text = "Crouch";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Place(grid.Find("Crouch"), 0, 228, 528, 96);
            if (page.Find("Crouch Mode") == null)
            {
                var card = UnityEngine.Object.Instantiate(root.transform.Find("Graphics/DropDowns/Quality").gameObject, page);
                card.name = "Crouch Mode";
                Place(card.transform, 696, 708, 1088, 96);
                var dropdown = card.GetComponentInChildren<TMP_Dropdown>(true);
                dropdown.name = "CrouchModeDrop";
                dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
                var callback = (UnityEngine.Events.UnityAction<int>)Delegate.CreateDelegate(
                    typeof(UnityEngine.Events.UnityAction<int>), menu, "UpdateCrouchMode");
                UnityEventTools.AddPersistentListener(dropdown.onValueChanged, callback);
                dropdown.ClearOptions();
                dropdown.AddOptions(new System.Collections.Generic.List<string> { "Hold", "Toggle" });
                Place(dropdown.transform, 560, 16, 504, 64);
                foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.transform.IsChildOf(dropdown.transform)) continue;
                    text.text = "Crouch Mode";
                    Place(text.transform, 24, 16, 510, 64);
                }
                var so = new SerializedObject(menu);
                so.FindProperty("crouchModeDropdown").objectReferenceValue = dropdown;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, SettingsPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return "Player references and shared Controls settings configured.";
    }

    private static void Place(Transform transform, float x, float y, float width, float height)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
