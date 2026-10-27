using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Base.BaseUpgrader;

public static class UpgradeHoverSetup
{
    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Exit Play Mode first.");
        const string path="Assets/Base/BaseUpgrader/BaseUpgrader.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var data=new SerializedObject(root.GetComponent<BaseUpgrader>());
            var cards=data.FindProperty("upgradeCards");
            for(int i=0;i<cards.arraySize;i++)
            {
                var card=cards.GetArrayElementAtIndex(i);
                var background=(Image)card.FindPropertyRelative("background").objectReferenceValue;
                background.raycastTarget=true;
                var hover=background.GetComponent<UpgradeOption>()??background.gameObject.AddComponent<UpgradeOption>();
                var fields=new SerializedObject(hover);
                fields.FindProperty("statusLabel").objectReferenceValue=card.FindPropertyRelative("statusLabel").objectReferenceValue;
                fields.ApplyModifiedPropertiesWithoutUndo();
                card.FindPropertyRelative("hoverFeedback").objectReferenceValue=hover;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,path);
            return "Wired full-card hover feedback on all six upgrade cards.";
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
}
