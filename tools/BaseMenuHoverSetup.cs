using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public static class BaseMenuHoverSetup
{
    public static string Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var menu = Resources.FindObjectsOfTypeAll<Steam.LobbyUiManager>()
            .Single(m => m.gameObject.scene.IsValid());
        var buttons = menu.transform.Find("Base").GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            var background = button.GetComponent<Image>();
            if (background == null) throw new InvalidOperationException(button.name + " has no background.");
            Undo.RecordObject(button, "Base menu hover strip");
            Undo.RecordObject(background, "Base menu hover strip");
            background.color = Color.white;
            background.raycastTarget = true;
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = new Color(.40f, .30f, .20f, 0f);
            colors.highlightedColor = new Color(.40f, .30f, .20f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.28f, .21f, .14f, 1f);
            colors.disabledColor = new Color(.12f, .12f, .14f, .25f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .12f;
            button.colors = colors;
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                Undo.RecordObject(label, "Base menu hover label");
                label.color = new Color(.922f, .914f, .878f, 1f);
                label.canvasRenderer.SetColor(Color.white);
                if (PrefabUtility.IsPartOfPrefabInstance(label))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
            if (PrefabUtility.IsPartOfPrefabInstance(button))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(button);
                PrefabUtility.RecordPrefabInstancePropertyModifications(background);
            }
        }
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
        var pointer = new PointerEventData(EventSystem.current);
        foreach (var button in buttons)
        {
            button.OnPointerEnter(pointer);
            if (button.targetGraphic.canvasRenderer.GetColor().a < .99f)
                throw new InvalidOperationException("Hover failed: " + button.name);
            button.OnPointerExit(pointer);
            if (button.targetGraphic.canvasRenderer.GetColor().a > .01f)
                throw new InvalidOperationException("Hover exit failed: " + button.name);
        }
        return "Saved Base hover strips. Pointer enter/exit passed for " + buttons.Length + " buttons; no click actions invoked.";
    }
}
