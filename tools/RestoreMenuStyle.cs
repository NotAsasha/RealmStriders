using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class RestoreMenuStyle
{
    private static Color Restore(Color c)
    {
        if(c.a==0)return c;
        if(c.r>c.b+.1f)return new Color(.855f,.694f,.459f,c.a);
        float v=c.r;
        if(v>.95f)return c;
        if(v>.8f)return new Color(.922f,.914f,.878f,c.a);
        if(v<.08f)return new Color(.043f,.043f,.055f,c.a);
        if(v>.15f && v<.4f)return new Color(v,v,v+.02f,c.a);
        return c;
    }
    private static void Style(GameObject root)
    {
        var title=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/CinzelDecorative-Bold SDF.asset");
        var body=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/SmoochSans-ExtraBold SDF.asset");
        foreach(var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            t.color=Restore(t.color);
            if(t.name=="Title")t.font=title;
            if(new[]{"Subtitle","Description","Detail","Warning","EmptyDetail"}.Contains(t.name))t.font=body;
        }
        foreach(var i in root.GetComponentsInChildren<Image>(true))i.color=Restore(i.color);
        foreach(var b in root.GetComponentsInChildren<Selectable>(true))
        {
            var c=b.colors;c.normalColor=Restore(c.normalColor);
            c.highlightedColor=b.targetGraphic is TMP_Text?new Color(.855f,.694f,.459f):new Color(.28f,.28f,.31f);
            c.selectedColor=c.highlightedColor;c.pressedColor=new Color(.7f,.55f,.35f);
            c.disabledColor=new Color(.63f,.64f,.64f);c.fadeDuration=.12f;b.colors=c;
        }
    }
    public static string Apply()
    {
        foreach(var path in new[]{"Assets/Player/UI/DropDown.prefab","Assets/Scenes/Menu/Save.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try{Style(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var m=Resources.FindObjectsOfTypeAll<Steam.LobbyUiManager>().Single(x=>x.gameObject.scene.IsValid());
        Style(m.gameObject);
        var so=new SerializedObject(m);
        so.FindProperty("selectedSaveColor").colorValue=new Color(.40f,.30f,.20f);
        so.FindProperty("normalSaveColor").colorValue=new Color(.15f,.14f,.16f);
        so.ApplyModifiedPropertiesWithoutUndo();
        foreach(var c in m.GetComponentsInChildren<Component>(true))
            if(c!=null && PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        EditorSceneManager.MarkSceneDirty(m.gameObject.scene);EditorSceneManager.SaveScene(m.gameObject.scene);
        return "Restored reference-page fonts and palette.";
    }
}
