using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steam;

public static class AlignRemainingMenus
{
    private static TMP_FontAsset titleFont,buttonFont,bodyFont,detailFont;
    private static Color foreground,backdrop,card;
    private static ColorBlock buttonColors;
    private static readonly Color Gray=new Color(.55f,.55f,.55f);
    private static void Place(Transform t,float x,float y,float w,float h)
    {
        var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
        r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
        r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
        r.localPosition=new Vector3(r.localPosition.x,r.localPosition.y,0);
    }
    private static void Stretch(Transform t)
    {
        var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;
        r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;
    }
    private static Image Fill(Transform t,Color color)
    {
        var image=t.GetComponent<Image>();if(image==null)image=t.gameObject.AddComponent<Image>();
        image.sprite=null;image.color=color;image.raycastTarget=false;return image;
    }
    private static void Style(TMP_Text t,TMP_FontAsset font,float size,Color color)
    {
        t.font=font;t.fontStyle=FontStyles.Normal;t.color=color;t.raycastTarget=false;
        t.enableAutoSizing=true;t.fontSizeMin=Mathf.Min(size,22);t.fontSizeMax=size;t.fontSize=size;
        t.alignment=TextAlignmentOptions.MidlineLeft;
    }
    private static TMP_Text Label(Transform parent,string name,string value,float x,float y,float w,float h)
    {
        var child=parent.Find(name);
        if(child==null)
        {
            child=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).transform;
            child.SetParent(parent,false);
        }
        Place(child,x,y,w,h);
        var text=child.GetComponent<TMP_Text>();text.text=value;Style(text,bodyFont,40,new Color(.63f,.64f,.64f));
        return text;
    }
    private static void Navigation(Button b,float x,float y,float w,string label=null)
    {
        Place(b.transform,x,y,w,75);
        var t=b.GetComponentInChildren<TMP_Text>(true);Stretch(t.transform);
        if(label!=null)t.text=label;
        Style(t,buttonFont,40,foreground);
        var image=b.GetComponent<Image>();image.sprite=null;image.color=Color.clear;image.raycastTarget=true;
        b.targetGraphic=t;b.transition=Selectable.Transition.ColorTint;b.colors=buttonColors;
    }
    private static void Title(Transform page,string text)
    {
        var t=page.Find("Title").GetComponent<TMP_Text>();t.text=text;
        Place(t.transform,136,156,1500,90);Style(t,titleFont,72,foreground);
    }

    private static void Settings(Transform root)
    {
        root.Find("Background").GetComponent<Image>().color=backdrop;
        Title(root,"Settings");
        root.Find("Title/TitleRule")?.gameObject.SetActive(false);
        Label(root,"Subtitle","Configure your equipment before you cross.",136,256,1500,50);
        var tabs=root.Find("CategoriesButtons");
        var layout=tabs.GetComponent<HorizontalLayoutGroup>();if(layout!=null)layout.enabled=false;
        Place(tabs,136,330,1648,75);
        for(int i=0;i<tabs.childCount;i++)
        {
            var b=tabs.GetChild(i).GetComponent<Button>();
            Place(b.transform,i*380,0,340,70);
            var t=b.GetComponentInChildren<TMP_Text>();Style(t,buttonFont,32,foreground);
            t.text=b.name.ToUpperInvariant();t.alignment=TextAlignmentOptions.Center;
            var indicator=b.transform.Find("ActiveIndicator");
            if(indicator!=null)indicator.GetComponent<Image>().color=buttonColors.highlightedColor;
        }
        var menu=root.GetComponent<Assets.Player.UI.SettingsMenu>();
        var so=new SerializedObject(menu);
        so.FindProperty("categoryNormalColor").colorValue=new Color(.043f,.043f,.055f,.3f);
        so.FindProperty("categoryActiveColor").colorValue=new Color(.22f,.22f,.24f,.9f);
        so.FindProperty("categoryHoverColor").colorValue=new Color(.26f,.26f,.28f,.65f);
        so.FindProperty("categoryActiveHoverColor").colorValue=new Color(.32f,.32f,.34f,.95f);
        so.FindProperty("voiceNormalColor").colorValue=card;
        so.FindProperty("voiceSelectedColor").colorValue=new Color(.40f,.30f,.20f);
        so.ApplyModifiedPropertiesWithoutUndo();
        foreach(string name in new[]{"Graphics","Sound","Controls"})
        {
            var page=root.Find(name);Stretch(page);Fill(page,Color.clear);
        }
        var graphics=root.Find("Graphics/DropDowns");
        graphics.GetComponent<GridLayoutGroup>().enabled=false;
        Place(graphics,136,440,1648,350);
        for(int i=0;i<graphics.childCount;i++)
        {
            var row=graphics.GetChild(i);Place(row,i%3*560,i/3*190,528,165);Fill(row,card);
            var dropdown=row.GetComponentInChildren<TMP_Dropdown>(true);
            var label=row.GetComponentsInChildren<TMP_Text>(true).First(t=>dropdown==null || !t.transform.IsChildOf(dropdown.transform));
            Place(label.transform,24,12,480,48);Style(label,buttonFont,27,foreground);
            if(dropdown!=null)
            {
                Place(dropdown.transform,24,78,480,64);
                dropdown.GetComponent<Image>().color=new Color(.20f,.20f,.22f);
                Place(dropdown.captionText.transform,16,0,410,64);
                Style(dropdown.captionText,detailFont,40,foreground);
                foreach(var text in dropdown.template.GetComponentsInChildren<TMP_Text>(true))
                    Style(text,detailFont,36,foreground);
                foreach(var image in dropdown.template.GetComponentsInChildren<Image>(true))
                    image.color=image.name.Contains("Checkmark")?foreground:new Color(.12f,.12f,.14f);
            }
            else
            {
                Place(row.Find("Background"),24,88,48,48);
                var toggle=row.GetComponent<Toggle>();toggle.targetGraphic.color=new Color(.2f,.2f,.22f);toggle.graphic.color=foreground;
            }
        }
        var volume=root.Find("Sound/Sound Volume");
        var sensitivity=root.Find("Controls/Mouse Sensativity");
        foreach(var row in new[]{volume,sensitivity})
        {
            Place(row,136,440,1648,110);Fill(row,card);
            var text=row.GetComponentInChildren<TMP_Text>();
            Place(text.transform,24,0,580,110);Style(text,buttonFont,32,foreground);
            var slider=row.GetComponentInChildren<Slider>();Place(slider.transform,650,35,950,40);
            foreach(var image in slider.GetComponentsInChildren<Image>(true))
                image.color=image.name=="Background"?new Color(.22f,.22f,.24f):foreground;
        }
        var voice=root.Find("Sound/Voice Mode");Place(voice,136,590,1648,160);Fill(voice,card);
        Place(voice.Find("Label"),24,12,580,136);Style(voice.Find("Label").GetComponent<TMP_Text>(),buttonFont,32,foreground);
        var choices=voice.Find("GameObject");
        var choiceLayout=choices.GetComponent<HorizontalLayoutGroup>();if(choiceLayout!=null)choiceLayout.enabled=false;
        Place(choices,650,40,950,75);
        var voiceButtons=choices.GetComponentsInChildren<Button>();
        for(int i=0;i<voiceButtons.Length;i++)
        {
            var b=voiceButtons[i];Place(b.transform,i*490,0,460,75);
            var text=b.GetComponentInChildren<TMP_Text>();Style(text,buttonFont,26,foreground);text.alignment=TextAlignmentOptions.Center;
        }
        var binds=root.Find("Controls/BindButtons");binds.GetComponent<GridLayoutGroup>().enabled=false;Place(binds,136,590,1648,210);
        for(int i=0;i<binds.childCount;i++)
        {
            var row=binds.GetChild(i);Place(row,i%3*560,i/3*114,528,96);Fill(row,card);
            var name=row.Find("Name").GetComponent<TMP_Text>();Place(name.transform,24,0,240,96);Style(name,buttonFont,25,foreground);
            var b=row.GetComponentInChildren<Button>();Place(b.transform,280,16,224,64);
            b.GetComponent<Image>().color=new Color(.20f,.20f,.22f);
            var text=b.GetComponentInChildren<TMP_Text>();Stretch(text.transform);Style(text,detailFont,32,foreground);text.alignment=TextAlignmentOptions.Center;
        }
        var footer=root.Find("Footer");Stretch(footer);
        Navigation(footer.Find("BackToMenu").GetComponent<Button>(),136,896,300,"<  BACK");
        Navigation(footer.Find("Apply").GetComponent<Button>(),771,896,400,"APPLY");
        menu.SelectCategory(0);
    }

    public static string Apply()
    {
        var menu=Resources.FindObjectsOfTypeAll<LobbyUiManager>().Single(m=>m.gameObject.scene.IsValid());
        var host=menu.transform.Find("SaveSelector");var main=menu.transform.Find("Base");
        titleFont=host.Find("Title").GetComponent<TMP_Text>().font;
        buttonFont=main.Find("HostLobby").GetComponentInChildren<TMP_Text>().font;
        bodyFont=host.Find("Subtitle").GetComponent<TMP_Text>().font;
        detailFont=host.Find("Saves/Save/SlotLabel").GetComponent<TMP_Text>().font;
        foreground=host.Find("Title").GetComponent<TMP_Text>().color;
        backdrop=host.GetComponent<Image>().color;
        card=host.Find("Saves/Save/Image").GetComponent<Image>().color;
        buttonColors=host.Find("Footer/Back").GetComponent<Button>().colors;
        string Fingerprint(Transform root)=>string.Join("\n",root.GetComponentsInChildren<Component>(true).Select(EditorJsonUtility.ToJson));
        var mainBefore=Fingerprint(main);var hostBefore=Fingerprint(host);
        Undo.RegisterFullObjectHierarchyUndo(menu.gameObject,"Match remaining menus to owner design");
        var join=menu.transform.Find("JoinLobby");Fill(join,backdrop);Title(join,"Join Lobby");
        join.Find("Eyebrow").gameObject.SetActive(false);
        Style(join.Find("Subtitle").GetComponent<TMP_Text>(),bodyFont,40,new Color(.63f,.64f,.64f));
        join.Find("HeaderRule").GetComponent<Image>().color=Gray;
        foreach(string name in new[]{"LobbyHeader","PlayersHeader","ConnectionHeader"})
            Style(join.Find(name).GetComponent<TMP_Text>(),detailFont,34,Gray);
        Fill(join.Find("EmptyList"),card);
        Style(join.Find("EmptyTitle").GetComponent<TMP_Text>(),buttonFont,34,foreground);
        Style(join.Find("EmptyDetail").GetComponent<TMP_Text>(),bodyFont,38,new Color(.63f,.64f,.64f));
        Navigation(join.Find("Back").GetComponent<Button>(),136,896,300);
        Navigation(join.Find("Refresh").GetComponent<Button>(),771,896,400);
        foreach(string name in new[]{"FileManagement","DeleteConfirmation"})
        {
            var dialog=menu.transform.Find(name);
            dialog.GetComponent<Image>().color=new Color(backdrop.r,backdrop.g,backdrop.b,1f);
            Title(dialog,name=="FileManagement"?"Manage Files":"Delete All Files?");
            foreach(var text in dialog.GetComponentsInChildren<TMP_Text>(true))
            {
                if(text.name=="Title" || text.GetComponentInParent<Button>()!=null)continue;
                Style(text,bodyFont,44,foreground);
            }
            if(name=="FileManagement")
            {
                Place(dialog.Find("Description"),136,366,1648,75);
                Place(dialog.Find("Detail"),136,458,1648,100);
            }
            else Place(dialog.Find("Warning"),136,366,1648,190);
            Navigation(dialog.Find("Cancel").GetComponent<Button>(),136,896,550,name=="FileManagement"?"<  BACK":"KEEP MY FILES");
            var danger=dialog.Find(name=="FileManagement"?"RequestDelete":"ConfirmDelete").GetComponent<Button>();
            Navigation(danger,990,896,790);
            if(name=="DeleteConfirmation")
            {
                var colors=danger.colors;colors.normalColor=new Color(.85f,.4f,.4f);
                colors.highlightedColor=new Color(1f,.55f,.5f);colors.selectedColor=colors.highlightedColor;danger.colors=colors;
            }
        }
        // Apply the common Settings layout to its asset and the existing scene instance.
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Player/UI/Settings.prefab");
        try {Settings(prefab.transform);PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Player/UI/Settings.prefab");}
        finally {PrefabUtility.UnloadPrefabContents(prefab);}
        Settings(menu.transform.Find("Settings"));
        foreach(var component in menu.transform.Find("Settings").GetComponentsInChildren<Component>(true))
            if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        if(mainBefore!=Fingerprint(main) || hostBefore!=Fingerprint(host))throw new Exception("Reference pages were unexpectedly changed.");
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
        return "Styled Settings, Join and both file dialogs. Base and SaveSelector verified unchanged; owner scene edits preserved.";
    }
}
