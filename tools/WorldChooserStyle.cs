using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Base.WorldChooser;

public static class WorldChooserStyle
{
    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first.");
        const string path="Assets/Base/WorldChooser/WorldCard.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var card=root.GetComponent<WorldCard>();var data=new SerializedObject(card);
            var background=root.GetComponent<Image>();background.color=new Color(.10f,.10f,.12f);
            var button=root.transform.Find("Buy").GetComponent<Button>();button.transition=Selectable.Transition.ColorTint;
            button.targetGraphic.color=Color.white;
            var colors=button.colors;colors.normalColor=new Color(.16f,.17f,.19f);colors.highlightedColor=new Color(.27f,.28f,.31f);
            colors.pressedColor=new Color(.10f,.11f,.13f);colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(.12f,.12f,.14f);
            colors.colorMultiplier=1;colors.fadeDuration=.12f;button.colors=colors;
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))text.color=new Color(.89f,.89f,.86f);
            var label=button.GetComponentInChildren<TMP_Text>();label.text="CHOOSE";label.enableAutoSizing=true;label.fontSizeMax=label.fontSize;label.fontSizeMin=16;
            var existing=root.transform.Find("SelectedIndicator");
            var indicator=existing!=null?existing.gameObject:new GameObject("SelectedIndicator",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            indicator.transform.SetParent(root.transform,false);var rect=(RectTransform)indicator.transform;
            rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,.5f);rect.offsetMin=new Vector2(0,0);rect.offsetMax=new Vector2(5,0);
            var stripe=indicator.GetComponent<Image>();stripe.color=new Color(.79f,.81f,.83f);stripe.raycastTarget=false;
            data.FindProperty("background").objectReferenceValue=background;data.FindProperty("selectedIndicator").objectReferenceValue=indicator;
            data.FindProperty("selectionLabel").objectReferenceValue=label;data.ApplyModifiedPropertiesWithoutUndo();
            card.RefreshSelection(-1);PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var chooser=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Base/WorldChooser/WorldChooser.prefab");
        foreach(var card in chooser.GetComponentsInChildren<WorldCard>(true))
            if(new SerializedObject(card).FindProperty("selectedIndicator").objectReferenceValue==null)throw new Exception("Nested card missing highlight reference");
        return "WorldCard uses dark buttons and graphite surfaces, light text, and a serialized selection stripe; WorldChooser inherits the card changes.";
    }
}
