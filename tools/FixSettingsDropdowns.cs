using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class FixSettingsDropdowns
{
    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);
        rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);
        rect.localScale=Vector3.one;
        rect.localPosition=new Vector3(rect.localPosition.x,rect.localPosition.y,0);
    }

    private static void Fix(TMP_Dropdown dropdown)
    {
        var template=dropdown.template;
        template.anchorMin=new Vector2(0,0);template.anchorMax=new Vector2(1,0);
        template.pivot=new Vector2(.5f,1);template.anchoredPosition=new Vector2(0,-4);
        template.sizeDelta=new Vector2(0,312);template.localScale=Vector3.one;
        var viewport=(RectTransform)template.Find("Viewport");
        Stretch(viewport,6,6,26,6);
        var content=(RectTransform)viewport.Find("Content");
        content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);
        content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
        content.sizeDelta=new Vector2(0,60);content.localScale=Vector3.one;
        var item=(RectTransform)content.Find("Item");
        Stretch(item,0,0,0,0);
        var background=(RectTransform)item.Find("Item Background");Stretch(background,0,0,0,0);
        var label=dropdown.itemText;
        Stretch(label.rectTransform,52,0,12,0);
        label.alignment=TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode=TextWrappingModes.NoWrap;
        label.enableAutoSizing=true;label.fontSizeMin=18;label.fontSizeMax=32;
        label.margin=Vector4.zero;label.color=new Color(.92f,.91f,.88f);label.raycastTarget=false;
        var check=(RectTransform)item.Find("Item Checkmark");
        check.anchorMin=check.anchorMax=new Vector2(0,.5f);
        check.pivot=new Vector2(.5f,.5f);check.anchoredPosition=new Vector2(24,0);
        check.sizeDelta=new Vector2(24,24);check.localScale=Vector3.one;
        check.GetComponent<Image>().color=new Color(.85f,.69f,.46f);
        var toggle=item.GetComponent<Toggle>();
        var image=background.GetComponent<Image>();
        image.color=Color.white;image.raycastTarget=true;toggle.targetGraphic=image;
        var colors=toggle.colors;
        colors.normalColor=new Color(.12f,.12f,.14f);
        colors.highlightedColor=new Color(.28f,.28f,.31f);
        colors.selectedColor=new Color(.40f,.30f,.20f);
        colors.pressedColor=new Color(.32f,.25f,.18f);
        colors.fadeDuration=.08f;toggle.colors=colors;
        template.GetComponent<Image>().color=new Color(.10f,.10f,.12f);
        var scroll=template.GetComponent<ScrollRect>();
        scroll.viewport=viewport;scroll.content=content;
        scroll.horizontal=false;scroll.vertical=true;
        // Reserve a stable scrollbar gutter so viewport expansion cannot rewrite anchors.
        scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalScrollbarSpacing=0;
        scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
        var bar=(RectTransform)template.Find("Scrollbar");
        bar.anchorMin=new Vector2(1,0);bar.anchorMax=Vector2.one;bar.pivot=new Vector2(1,.5f);
        bar.offsetMin=new Vector2(-20,6);bar.offsetMax=new Vector2(-4,-6);
        var sliding=(RectTransform)bar.Find("Sliding Area");Stretch(sliding,2,2,2,2);
        var handle=(RectTransform)sliding.Find("Handle");Stretch(handle,0,0,0,0);
        handle.GetComponent<Image>().color=new Color(.42f,.42f,.45f);
        var scrollbar=bar.GetComponent<Scrollbar>();
        scrollbar.direction=Scrollbar.Direction.BottomToTop;scrollbar.size=1;scrollbar.value=1;
        scroll.verticalScrollbar=scrollbar;
        template.gameObject.SetActive(false);
    }

    public static string Apply()
    {
        foreach(string path in new[]{"Assets/Player/UI/DropDown.prefab","Assets/Player/UI/Settings.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))Fix(dropdown);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        var menu=Resources.FindObjectsOfTypeAll<Steam.LobbyUiManager>().FirstOrDefault(m=>m.gameObject.scene.IsValid());
        if(menu!=null)
        {
            foreach(var dropdown in menu.transform.Find("Settings").GetComponentsInChildren<TMP_Dropdown>(true))
            {
                Fix(dropdown);
                foreach(var component in dropdown.template.GetComponentsInChildren<Component>(true))
                    if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorSceneManager.SaveScene(menu.gameObject.scene);
        }
        return "Fixed dropdown item sizing, left-aligned labels, viewport anchors and scrollbar gutter in prefab and Menu instance.";
    }

}
