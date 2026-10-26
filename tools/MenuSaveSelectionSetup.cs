using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Steam;
using Scenes;

public static class MenuSaveSelectionSetup
{
    private static TMP_FontAsset heading;
    private static TMP_FontAsset body;
    private static readonly Color Accent=new Color32(218,177,117,255);

    private static RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
    {
        var t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        t.SetParent(parent,false);
        t.anchorMin=t.anchorMax=t.pivot=new Vector2(0,1);
        t.anchoredPosition=new Vector2(x,-y);t.sizeDelta=new Vector2(w,h);
        return t;
    }

    private static TMP_Text Text(Transform parent,string name,string value,float x,float y,float w,float h,float size,bool title=false)
    {
        var text=Box(parent,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        text.text=value;text.font=title?heading:body;text.fontSize=size;
        text.color=title?Accent:new Color32(239,231,219,255);
        text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;
        return text;
    }

    private static Button Button(Transform parent,string name,string value,float x,UnityAction click)
    {
        var r=Box(parent,name,x,680,470,68);
        var image=r.gameObject.AddComponent<Image>();image.color=new Color32(59,52,65,255);
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;
        Text(r,"Label",value,20,0,430,68,36);
        var colors=button.colors;colors.highlightedColor=new Color(1.3f,1.2f,1.1f);
        colors.selectedColor=colors.highlightedColor;button.colors=colors;
        UnityEventTools.AddPersistentListener(button.onClick,click);
        return button;
    }

    private static GameObject Dialog(Transform parent,string name)
    {
        var r=Box(parent,name,0,0,1920,1080);
        r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        var canvas=r.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting=true;canvas.sortingOrder=100;
        r.gameObject.AddComponent<GraphicRaycaster>();
        var shade=r.gameObject.AddComponent<Image>();shade.color=new Color(0.035f,0.03f,0.045f,.98f);
        shade.raycastTarget=true;
        r.gameObject.SetActive(false);
        return r.gameObject;
    }

    private static void Fonts(GameObject root)
    {
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            bool isHeading=text.name=="Title" || text.transform.parent?.name=="CategoriesButtons";
            var parent=text.transform.parent;
            if(parent!=null && parent.parent!=null && parent.parent.name=="CategoriesButtons")isHeading=true;
            text.font=isHeading?heading:body;
            text.fontStyle=FontStyles.Normal;
            if(isHeading && text.name=="Title")
            {
                text.enableAutoSizing=true;text.fontSizeMax=72;text.fontSizeMin=38;
            }
            else
            {
                text.enableAutoSizing=true;
                text.fontSizeMax=Mathf.Max(text.fontSize,isHeading?38:32);
                text.fontSizeMin=isHeading?24:22;
            }
        }
    }

    public static string Apply()
    {
        heading=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/CinzelDecorative-Bold SDF.asset");
        body=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/SmoochSans-ExtraBold SDF.asset");
        var menu=Resources.FindObjectsOfTypeAll<LobbyUiManager>().Single(m=>m.gameObject.scene.IsValid());
        if(menu.transform.Find("FileManagement")!=null)throw new Exception("Already configured.");
        var root=menu.transform;
        var host=root.Find("SaveSelector");
        var create=host.Find("Footer/Create Lobby").GetComponent<Button>();
        var so=new SerializedObject(menu);
        var slots=so.FindProperty("saveSlots");slots.arraySize=3;
        var saves=host.Find("Saves");
        for(int i=0;i<3;i++)
        {
            var card=saves.GetChild(i);
            var source=card.GetComponentInChildren<LoadSaveButton>(true);
            var button=source.GetComponent<Button>();
            for(int j=button.onClick.GetPersistentEventCount()-1;j>=0;j--)
                if(button.onClick.GetPersistentMethodName(j)=="LoadSave")
                    UnityEventTools.RemovePersistentListener(button.onClick,j);
            UnityEventTools.AddIntPersistentListener(button.onClick,menu.SelectSave,i);
            var slot=slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("source").objectReferenceValue=source;
            slot.FindPropertyRelative("background").objectReferenceValue=card.Find("Image").GetComponent<Image>();
            slot.FindPropertyRelative("selectionLabel").objectReferenceValue=button.GetComponentInChildren<TMP_Text>();
        }
        so.FindProperty("createLobbyButton").objectReferenceValue=create;
        var status=Text(host,"HostStatus","Select a save to create a lobby.",740,762,1040,52,32);
        so.FindProperty("hostStatus").objectReferenceValue=status;
        so.ApplyModifiedPropertiesWithoutUndo();

        var started=(UnityEvent)typeof(LobbyUiManager).GetField("onHostStarted",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
        for(int i=0;i<create.onClick.GetPersistentEventCount();i++)
        {
            var target=create.onClick.GetPersistentTarget(i);
            string method=create.onClick.GetPersistentMethodName(i);
            if(target is SceneLoader loader) UnityEventTools.AddPersistentListener(started,loader.LoadScene);
            else if(target is GameObject go && method=="SetActive") UnityEventTools.AddBoolPersistentListener(started,go.SetActive,false);
            else if(target is AudioSource audio) UnityEventTools.AddPersistentListener(started,audio.Play);
        }
        for(int i=create.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(create.onClick,i);
        UnityEventTools.AddPersistentListener(create.onClick,menu.CreateLobbyButton);
        create.interactable=false;

        var management=Dialog(root,"FileManagement");
        Text(management.transform,"Title","MANAGE FILES",420,274,1100,110,60,true);
        Text(management.transform,"Description","Reset local game data",420,420,1100,70,42);
        Text(management.transform,"Detail","This includes all save slots and local settings.",420,508,1100,90,34);
        var manageCancel=Button(management.transform,"Cancel","BACK",420,menu.CloseFileManagement);
        Button(management.transform,"RequestDelete","DELETE ALL FILES...",1030,menu.RequestDeleteFiles);
        var confirmation=Dialog(root,"DeleteConfirmation");
        Text(confirmation.transform,"Title","DELETE ALL FILES?",420,274,1100,110,56,true);
        Text(confirmation.transform,"Warning","All saved progress and settings will be reset.\nThis cannot be undone.",420,420,1100,170,38);
        var confirmCancel=Button(confirmation.transform,"Cancel","KEEP MY FILES",420,menu.CloseFileManagement);
        var confirmDelete=Button(confirmation.transform,"ConfirmDelete","DELETE ALL FILES",1030,menu.DeleteFiles);
        confirmDelete.image.color=new Color32(110,51,47,255);
        var manage=host.Find("Delete").GetComponent<Button>();
        for(int i=manage.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(manage.onClick,i);
        UnityEventTools.AddPersistentListener(manage.onClick,menu.OpenFileManagement);
        manage.GetComponentInChildren<TMP_Text>().text="MANAGE FILES";
        so.Update();
        so.FindProperty("fileManagementDialog").objectReferenceValue=management;
        so.FindProperty("deleteConfirmationDialog").objectReferenceValue=confirmation;
        so.FindProperty("manageFilesButton").objectReferenceValue=manage;
        so.FindProperty("managementCancel").objectReferenceValue=manageCancel;
        so.FindProperty("confirmationCancel").objectReferenceValue=confirmCancel;
        so.ApplyModifiedPropertiesWithoutUndo();
        Fonts(menu.gameObject);
        // Common reusable menu prefabs keep the same two-font family in-game.
        foreach(string path in new[]{"Assets/Player/UI/Settings.prefab","Assets/Player/UI/PauseMenu.prefab",
            "Assets/Scenes/Menu/Save.prefab","Assets/Player/UI/BindButton.prefab","Assets/Player/UI/DropDown.prefab"})
        {
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try {Fonts(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,path);}
            finally {PrefabUtility.UnloadPrefabContents(prefab);}
        }
        foreach(var component in menu.GetComponentsInChildren<Component>(true))
            if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
        return "Save selection wired, Create gated, deletion moved behind management and confirmation, original fonts restored.";
    }

    public static string FinalizeScene()
    {
        heading=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/CinzelDecorative-Bold SDF.asset");
        body=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/SmoochSans-ExtraBold SDF.asset");
        var menu=Resources.FindObjectsOfTypeAll<LobbyUiManager>().Single(m=>m.gameObject.scene.IsValid());
        Fonts(menu.gameObject);
        var saves=menu.transform.Find("SaveSelector/Saves");
        for(int i=0;i<saves.childCount;i++)
        {
            var button=saves.GetChild(i).GetComponentInChildren<LoadSaveButton>(true).GetComponent<Button>();
            for(int j=button.onClick.GetPersistentEventCount()-1;j>=0;j--)
                if(!(button.onClick.GetPersistentTarget(j) is AudioSource))
                    UnityEventTools.RemovePersistentListener(button.onClick,j);
            UnityEventTools.AddIntPersistentListener(button.onClick,menu.SelectSave,i);
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        }
        foreach(var text in menu.GetComponentsInChildren<TMP_Text>(true))
        {
            if(text.name!="Title")
            {
                text.fontSizeMax=text.transform.parent!=null && text.transform.parent.parent!=null &&
                    text.transform.parent.parent.name=="CategoriesButtons"?38:40;
                text.fontSizeMin=22;
            }
            if(PrefabUtility.IsPartOfPrefabInstance(text))PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
        return "Save callbacks persisted after shared prefab font refresh.";
    }
}
