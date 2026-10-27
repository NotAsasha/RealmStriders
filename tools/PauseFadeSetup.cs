using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Player.UI;

public static class PauseFadeSetup
{
    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first.");
        const string path="Assets/Player/UI/PauseMenu.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var manager=root.GetComponent<PauseUIManager>();
            var group=root.GetComponent<CanvasGroup>();
            if(group==null)group=root.AddComponent<CanvasGroup>();
            group.alpha=0;group.interactable=false;group.blocksRaycasts=false;group.ignoreParentGroups=false;
            var data=new SerializedObject(manager);
            data.FindProperty("pauseCanvasGroup").objectReferenceValue=group;
            data.FindProperty("fadeInDuration").floatValue=.24f;
            data.FindProperty("fadeOutDuration").floatValue=.16f;
            data.ApplyModifiedPropertiesWithoutUndo();
            var oldEffect=root.GetComponentInChildren<ScreenShatterEffect>(true);
            if(oldEffect!=null)UnityEngine.Object.DestroyImmediate(oldEffect.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab");
        var pause=player.GetComponentInChildren<PauseUIManager>(true);
        var inherited=new SerializedObject(pause).FindProperty("pauseCanvasGroup").objectReferenceValue;
        if(inherited==null || player.GetComponentsInChildren<ScreenShatterEffect>(true).Length!=0)
            throw new Exception("Player prefab did not inherit the fade setup.");
        return "Pause prefab has a configured CanvasGroup; removed its shatter effect. Player prefab inherits both changes.";
    }
}
