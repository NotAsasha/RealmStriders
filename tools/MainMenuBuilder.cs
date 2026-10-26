using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Steam;

public static class MainMenuBuilder
{
    private static readonly Color Text = new Color32(235, 233, 224, 255);
    private static readonly Color Muted = new Color32(161, 164, 163, 255);
    private static readonly Color Accent = new Color32(218, 177, 117, 255);
    private static readonly Color Surface = new Color32(38, 36, 42, 255);
    private static TMP_FontAsset font;

    private static void Position(Transform t, float x, float y, float width, float height)
    {
        var rect = (RectTransform)t;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, 0);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void Stretch(Transform t)
    {
        var r = (RectTransform)t;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero; r.localScale = Vector3.one;
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        Position(obj.transform, x, y, w, h);
        return (RectTransform)obj.transform;
    }

    private static Image Fill(Transform t, Color color)
    {
        var image = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
        image.sprite = null; image.color = color; image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Label(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color)
    {
        var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(text, value, size, color);
        return text;
    }

    private static void ConfigureText(TMP_Text t, string value, float size, Color color)
    {
        t.font = font; t.text = value; t.fontSize = size; t.enableAutoSizing = false;
        t.fontStyle = FontStyles.Normal; t.alignment = TextAlignmentOptions.MidlineLeft;
        t.color = color; t.raycastTarget = false;
    }

    private static void NavigationButton(Button b, string title, float x, float y, float width, UnityAction action)
    {
        Position(b.transform, x, y, width, 64);
        var image = b.image;
        image.sprite = null; image.color = Color.clear; image.raycastTarget = true;
        var label = b.GetComponentInChildren<TMP_Text>(true);
        Stretch(label.transform);
        ConfigureText(label, title, 30, Text);
        b.targetGraphic = label;
        b.transition = Selectable.Transition.ColorTint;
        var colors = b.colors;
        colors.normalColor = Text; colors.highlightedColor = Accent;
        colors.selectedColor = Accent; colors.pressedColor = new Color(.7f, .55f, .35f);
        colors.disabledColor = Muted; colors.fadeDuration = .12f;
        b.colors = colors;
        if (action != null)
        {
            // Replace old page toggles while retaining unrelated audio events.
            for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                if (!(b.onClick.GetPersistentTarget(i) is AudioSource))
                    UnityEventTools.RemovePersistentListener(b.onClick, i);
            UnityEventTools.AddPersistentListener(b.onClick, action);
        }
    }

    private static Button NewButton(Transform parent, string name, string title, float x, float y, float width, UnityAction action)
    {
        var rect = Rect(parent, name, x, y, width, 64);
        var image = Fill(rect, Color.clear);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Label(rect, "Label", title, 0, 0, width, 64, 30, Text);
        NavigationButton(button, title, x, y, width, action);
        return button;
    }

    public static string Build()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Menu/Menu.unity" || scene.isDirty)
            throw new InvalidOperationException("Expected the saved Menu scene.");
        var menu = GameObject.Find("MenuCamera/LobbyScreen/Menu");
        if (menu.transform.Find("JoinLobby") != null) throw new InvalidOperationException("Menu already migrated.");
        var manager = menu.GetComponent<LobbyUiManager>();
        var main = menu.transform.Find("Base");
        var host = menu.transform.Find("SaveSelector");
        var settings = menu.transform.Find("Settings");
        Undo.RegisterFullObjectHierarchyUndo(menu, "Restyle main menu");
        Stretch(menu.transform); Stretch(main); Stretch(host);
        Fill(menu.transform, Color.clear);
        var scaler = menu.GetComponentInParent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var shade = Rect(main,"NavigationShade",0,0,730,1080);
        Fill(shade,new Color(0.045f,0.045f,0.055f,.80f));
        shade.SetAsFirstSibling();
        var title = main.Find("Title").GetComponent<TMP_Text>();
        Position(title.transform,132,174,610,178);
        ConfigureText(title,"REALM\nSTRIDERS",72,Text);
        title.lineSpacing = -8;
        Label(main,"Subtitle","COOPERATIVE MONSTER HUNTING",136,370,590,40,18,Muted);
        Fill(Rect(main,"AccentRule",136,434,60,3),Accent);
        var hostButton=main.Find("Play").GetComponent<Button>();
        hostButton.name="HostLobby";
        NavigationButton(hostButton,"HOST LOBBY",136,472,460,manager.ShowHostLobby);
        NewButton(main,"JoinLobby","JOIN LOBBY",136,548,460,manager.ShowJoinLobby);
        NavigationButton(main.Find("ToSettings").GetComponent<Button>(),"SETTINGS",136,624,460,manager.ShowSettings);
        NewButton(main,"Exit","EXIT",136,734,460,manager.ExitGame);

        var join=Rect(menu.transform,"JoinLobby",0,0,1920,1080);
        Stretch(join);
        Fill(join,new Color(.07f,.065f,.085f,.96f));
        Label(join,"Eyebrow","REALM STRIDERS  /  MULTIPLAYER",136,94,1300,40,18,Accent);
        Label(join,"Title","JOIN LOBBY",136,156,1500,90,58,Text);
        Label(join,"Subtitle","Find a crew. Enter the unknown together.",136,256,1500,50,25,Muted);
        Fill(Rect(join,"HeaderRule",136,348,1648,2),new Color(.35f,.34f,.36f));
        Label(join,"LobbyHeader","LOBBY",160,366,1000,54,19,Muted);
        Label(join,"PlayersHeader","PLAYERS",1330,366,200,54,19,Muted);
        Label(join,"ConnectionHeader","CONNECTION",1550,366,230,54,19,Muted);
        Fill(Rect(join,"EmptyList",136,436,1648,372),new Color(.13f,.12f,.15f,.8f));
        Label(join,"EmptyTitle","Public lobbies are coming soon.",184,526,1500,62,32,Text);
        Label(join,"EmptyDetail","Lobby browsing is not available yet.",184,598,1450,46,23,Muted);
        NewButton(join,"Back","<  BACK",136,896,300,manager.ShowMainMenu);
        var refresh=NewButton(join,"Refresh","REFRESH",1440,896,330,null);
        refresh.interactable=false;

        Fill(host,new Color(.07f,.065f,.085f,.96f));
        Position(host.Find("Title"),136,156,1500,90);
        ConfigureText(host.Find("Title").GetComponent<TMP_Text>(),"HOST LOBBY",58,Text);
        Label(host,"Eyebrow","REALM STRIDERS  /  MULTIPLAYER",136,94,1300,40,18,Accent);
        Label(host,"Subtitle","Select a save, then create your lobby.",136,256,1500,50,25,Muted);
        var saves=host.Find("Saves");
        Position(saves,136,366,1648,330);
        var layout=saves.GetComponent<HorizontalLayoutGroup>();
        layout.spacing=32; layout.padding=new RectOffset(0,0,0,0);
        layout.childControlWidth=true; layout.childControlHeight=true;
        layout.childForceExpandWidth=true; layout.childForceExpandHeight=true;
        for(int i=0;i<saves.childCount;i++)
        {
            var card=saves.GetChild(i);
            Fill(card.Find("Image"),Surface);
            Stretch(card.Find("Image"));
            var saveText=card.Find("Text (TMP)").GetComponent<TMP_Text>();
            Position(saveText.transform,28,72,440,136);
            ConfigureText(saveText,saveText.text,28,Text);
            Label(card,"SlotLabel","SAVE 0"+(i+1),28,20,430,40,18,Accent);
            NavigationButton(card.Find("LoadSave").GetComponent<Button>(),"SELECT",28,240,420,null);
        }
        var toggle=host.Find("Toggle");
        Position(toggle,136,752,550,60);
        var toggleLabel=toggle.GetComponentInChildren<TMP_Text>();
        ConfigureText(toggleLabel,"Friends only",26,Text);
        Position(toggleLabel.transform,58,0,480,60);
        Position(toggle.Find("Background"),0,14,30,30);
        toggle.Find("Background").GetComponent<Image>().color=Surface;
        toggle.GetComponent<Toggle>().graphic.color=Accent;
        var footer=host.Find("Footer"); Stretch(footer);
        NavigationButton(footer.Find("Back").GetComponent<Button>(),"<  BACK",136,896,300,manager.ShowMainMenu);
        NavigationButton(footer.Find("Create Lobby").GetComponent<Button>(),"CREATE LOBBY",1390,896,400,null);
        var delete=menu.transform.Find("Delete");
        delete.SetParent(host,false);
        NavigationButton(delete.GetComponent<Button>(),"DELETE ALL SAVES",136,980,400,null);
        delete.GetComponentInChildren<TMP_Text>().fontSize=18;
        var version=menu.transform.Find("Version").GetComponent<TMP_Text>();
        Position(version.transform,1640,1010,220,40);
        ConfigureText(version,version.text,18,Muted);
        var offline=menu.transform.Find("SteamActive").GetComponent<TMP_Text>();
        Position(offline.transform,136,1010,1350,40);
        ConfigureText(offline,"Steam offline",18,Muted);
        // Keep this status on the main screen, away from host controls.
        offline.transform.SetParent(main,true);

        var so=new SerializedObject(manager);
        so.FindProperty("mainPage").objectReferenceValue=main.gameObject;
        so.FindProperty("hostPage").objectReferenceValue=host.gameObject;
        so.FindProperty("joinPage").objectReferenceValue=join.gameObject;
        so.FindProperty("settingsPage").objectReferenceValue=settings.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        var back=settings.Find("Footer/BackToMenu").GetComponent<Button>();
        for(int i=back.onClick.GetPersistentEventCount()-1;i>=0;i--)
            if(back.onClick.GetPersistentTarget(i) is GameObject)
                UnityEventTools.RemovePersistentListener(back.onClick,i);
        UnityEventTools.AddPersistentListener(back.onClick,manager.ShowMainMenu);
        PrefabUtility.RecordPrefabInstancePropertyModifications(back);
        manager.ShowMainMenu();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        StyleSettings();
        return "Main navigation, Host, Join placeholder, Exit and shared typography saved.";
    }

    private static void StyleSettings()
    {
        var root=PrefabUtility.LoadPrefabContents("Assets/Player/UI/Settings.prefab");
        try
        {
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font=font; text.fontStyle=FontStyles.Normal;
                text.enableAutoSizing=true; text.fontSizeMin=22; text.fontSizeMax=40;
            }
            var title=root.transform.Find("Title").GetComponent<TMP_Text>();
            title.text="SETTINGS"; title.enableAutoSizing=false; title.fontSize=58;
            foreach(var button in root.transform.Find("CategoriesButtons").GetComponentsInChildren<Button>())
            {
                var label=button.GetComponentInChildren<TMP_Text>();
                label.fontSizeMax=32; label.fontSizeMin=24;
            }
            foreach(var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                dropdown.captionText.fontSizeMin=18; dropdown.captionText.fontSizeMax=30;
                dropdown.itemText.fontSizeMin=18; dropdown.itemText.fontSizeMax=30;
            }
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Player/UI/Settings.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static string FixLayout()
    {
        var menu=GameObject.Find("MenuCamera/LobbyScreen/Menu");
        var host=menu.transform.Find("SaveSelector");
        var footer=host.Find("Footer");
        footer.GetComponent<HorizontalLayoutGroup>().enabled=false;
        Position(footer.Find("Back"),136,896,300,64);
        Position(footer.Find("Create Lobby"),1390,896,400,64);
        foreach(Transform card in host.Find("Saves"))
        {
            card.localScale=Vector3.one;
            var element=card.GetComponent<LayoutElement>() ?? card.gameObject.AddComponent<LayoutElement>();
            element.minWidth=0; element.preferredWidth=520; element.flexibleWidth=1;
        }
        var settings=menu.transform.Find("Settings");
        foreach(var rect in settings.GetComponentsInChildren<RectTransform>(true))
            if(rect.transform!=settings)
                PrefabUtility.RevertObjectOverride(rect,InteractionMode.AutomatedAction);
        Stretch(settings);
        PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        settings.Find("Background").gameObject.SetActive(true);
        PrefabUtility.RecordPrefabInstancePropertyModifications(settings.Find("Background").gameObject);
        EditorSceneManager.MarkSceneDirty(menu.scene);
        EditorSceneManager.SaveScene(menu.scene);
        return "Host footer layout corrected; Settings uses shared prefab layout and backdrop.";
    }

    public static string FinalizeLayout()
    {
        var menu=GameObject.Find("MenuCamera/LobbyScreen/Menu");
        var saves=menu.transform.Find("SaveSelector/Saves");
        saves.GetComponent<HorizontalLayoutGroup>().enabled=false;
        for(int i=0;i<saves.childCount;i++)
            Position(saves.GetChild(i),i*560,0,528,330);
        menu.transform.Find("SaveSelector/Delete").GetComponentInChildren<TMP_Text>().text="DELETE ALL FILES";
        foreach(var component in menu.GetComponentsInChildren<Component>(true))
            if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        foreach(var t in menu.GetComponentsInChildren<Transform>(true))
            if(PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        EditorSceneManager.MarkSceneDirty(menu.scene);
        EditorSceneManager.SaveScene(menu.scene);
        return "Aligned save cards and persisted nested prefab overrides.";
    }
}
