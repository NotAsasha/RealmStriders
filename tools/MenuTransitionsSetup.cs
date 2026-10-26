using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Steam;
using Assets.Player.UI;

public static class MenuTransitionsSetup
{
    public static string Configure()
    {
        var menu=Resources.FindObjectsOfTypeAll<LobbyUiManager>().Single(x=>x.gameObject.scene.IsValid());
        Undo.RegisterFullObjectHierarchyUndo(menu.gameObject,"Add menu transitions");
        var so=new SerializedObject(menu);
        var pages=so.FindProperty("pagePresentations");
        pages.arraySize=4;
        string[] paths={"Base","SaveSelector","JoinLobby","Settings"};
        string[] selections={"HostLobby","Saves/Save/LoadSave","Back","CategoriesButtons/Graphics"};
        for(int i=0;i<paths.Length;i++)
        {
            var page=menu.transform.Find(paths[i]);
            var group=page.GetComponent<CanvasGroup>();
            if(group==null) group=page.gameObject.AddComponent<CanvasGroup>();
            group.alpha=1; group.interactable=true; group.blocksRaycasts=true;
            var entry=pages.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("page").objectReferenceValue=page.gameObject;
            entry.FindPropertyRelative("group").objectReferenceValue=group;
            entry.FindPropertyRelative("title").objectReferenceValue=page.Find("Title").GetComponent<RectTransform>();
            entry.FindPropertyRelative("firstSelection").objectReferenceValue=page.Find(selections[i]).GetComponent<Selectable>();
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        var settings=menu.transform.Find("Settings").GetComponent<SettingsMenu>();
        for(int i=settings.OnClose.GetPersistentEventCount()-1;i>=0;i--)
            UnityEventTools.RemovePersistentListener(settings.OnClose,i);
        UnityEventTools.AddPersistentListener(settings.OnClose,menu.ShowMainMenu);
        var back=settings.transform.Find("Footer/BackToMenu").GetComponent<Button>();
        for(int i=back.onClick.GetPersistentEventCount()-1;i>=0;i--)
            if(back.onClick.GetPersistentTarget(i)==menu)
                UnityEventTools.RemovePersistentListener(back.onClick,i);
        PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        PrefabUtility.RecordPrefabInstancePropertyModifications(back);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
        return "Four serialized CanvasGroups and title targets assigned; Settings close routed through transition.";
    }
}
