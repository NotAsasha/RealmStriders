using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class DarkenMenuAccents
{
    private static bool IsWarmAccent(Color c) => c.r - c.b > .12f && c.g - c.b > .06f;

    private static int UpdatePalette(GameObject root, bool verify = false)
    {
        int count = 0;
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (!(component is Graphic || component is Selectable ||
                  component is Steam.LobbyUiManager || component is Assets.Player.UI.SettingsMenu)) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.Color || !IsWarmAccent(property.colorValue)) continue;
                count++;
                if (verify) continue;
                float alpha = property.colorValue.a;
                bool foreground = component is TMP_Text ||
                    component is Selectable selectable && selectable.targetGraphic is TMP_Text;
                bool symbol = component is Image &&
                    (component.name == "Arrow" || component.name.Contains("Checkmark"));
                property.colorValue = foreground ? new Color(.78f, .78f, .80f, alpha)
                    : symbol ? new Color(.55f, .56f, .59f, alpha)
                    : new Color(.24f, .25f, .28f, alpha);
            }
            if (!verify)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
        return count;
    }

    public static string Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        int changed = 0;
        foreach (string path in new[]{"Assets/Player/UI/DropDown.prefab", "Assets/Player/UI/Settings.prefab", "Assets/Scenes/Menu/Save.prefab"})
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                changed += UpdatePalette(root);
                if (UpdatePalette(root, true) != 0) throw new Exception("Warm accent remains: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var menu = Resources.FindObjectsOfTypeAll<Steam.LobbyUiManager>().Single(m=>m.gameObject.scene.IsValid());
        Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Dark menu accents");
        changed += UpdatePalette(menu.gameObject);
        if (UpdatePalette(menu.gameObject, true) != 0) throw new Exception("Warm accent remains in Menu.");
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
        return "Replaced " + changed + " warm color properties. Verified scene and three prefabs have no remaining yellow/brown accents; text and symbols use readable neutral gray.";
    }
}
