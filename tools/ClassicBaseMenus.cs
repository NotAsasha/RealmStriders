using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class ClassicBaseMenus
{
    private static readonly Color Face = new Color32(192,195,199,255);
    private static readonly Color White = new Color32(248,248,248,255);
    private static readonly Color Teal = new Color32(0,128,128,255);
    private static readonly Color Caption = new Color32(128,132,138,255);
    private static readonly Color Ink = new Color32(16,20,24,255);
    private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    private static T Ensure<T>(GameObject go) where T:Component {var c=go.GetComponent<T>();return c!=null?c:go.AddComponent<T>();}
    private static RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
    {
        var r=parent.Find(name) as RectTransform;
        if(r==null){r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);}
        Center(r,x,y,w,h);return r;
    }
    private static void Center(Transform t,float x,float y,float w,float h)
    {
        var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        r.sizeDelta=new Vector2(w,h);r.anchoredPosition3D=new Vector3(x,y,-.1f);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
    }
    private static void Top(Transform t,float x,float y,float w,float h)
    {
        var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
        r.sizeDelta=new Vector2(w,h);r.anchoredPosition3D=new Vector3(x,-y,0);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
    }
    private static Image Fill(Transform t,Color color,bool hit=false)
    {
        var i=Ensure<Image>(t.gameObject);i.sprite=null;i.color=color;i.raycastTarget=hit;return i;
    }
    private static void Border(Transform t,bool inset)
    {
        var r=(RectTransform)t;float w=r.rect.width,h=r.rect.height;
        Color light=inset?Color.gray:Color.white,dark=inset?Color.white:new Color(.12f,.12f,.12f);
        Fill(Box(t,"BevelTop",0,h/2-1.5f,w,3),light);Fill(Box(t,"BevelLeft",-w/2+1.5f,0,3,h),light);
        Fill(Box(t,"BevelBottom",0,-h/2+1.5f,w,3),dark);Fill(Box(t,"BevelRight",w/2-1.5f,0,3,h),dark);
        light=inset?new Color(.12f,.12f,.12f):Face;dark=inset?Face:Color.gray;
        Fill(Box(t,"InnerTop",0,h/2-4.5f,w-6,3),light);Fill(Box(t,"InnerLeft",-w/2+4.5f,0,3,h-6),light);
        Fill(Box(t,"InnerBottom",0,-h/2+4.5f,w-6,3),dark);Fill(Box(t,"InnerRight",w/2-4.5f,0,3,h-6),dark);
    }
    private static void Type(TMP_Text t,float size,bool bold=false)
    {
        t.font=Font;t.fontSharedMaterial=Font.material;t.color=Ink;t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;
        t.characterSpacing=0;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=size-2;t.enableAutoSizing=true;t.raycastTarget=false;
    }
    private static TMP_Text Label(Transform parent,string name,string value,float x,float y,float w,float h,float size,bool bold=false)
    {
        var t=Ensure<TextMeshProUGUI>(Box(parent,name,x,y,w,h).gameObject);Type(t,size,bold);t.text=value;t.alignment=TextAlignmentOptions.MidlineLeft;return t;
    }
    private static void ButtonStyle(Button b)
    {
        b.targetGraphic=Fill(b.transform,Color.white,true);b.transition=Selectable.Transition.ColorTint;
        var c=b.colors;c.normalColor=Face;c.highlightedColor=new Color32(213,216,220,255);c.selectedColor=c.normalColor;
        c.pressedColor=new Color32(160,163,168,255);c.disabledColor=new Color32(174,177,181,255);c.fadeDuration=0;c.colorMultiplier=1;b.colors=c;
        Border(b.transform,false);var t=b.GetComponentInChildren<TMP_Text>();Type(t,24);t.alignment=TextAlignmentOptions.Center;
    }
    private static void ColorField(UnityEngine.Object o,string field,Color value)
    {
        var s=new SerializedObject(o);s.FindProperty(field).colorValue=value;s.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Frame(Transform canvas,float width,float height,string title)
    {
        var back=Box(canvas,"Backdrop",0,0,width,height);Fill(back,Teal);back.SetAsFirstSibling();
        var panel=Box(canvas,"ClassicWindow",0,0,width-28,height-28);Fill(panel,Face);Border(panel,false);panel.SetSiblingIndex(1);
        var caption=Box(canvas,"TitleBar",0,height/2-52,width-56,50);Fill(caption,Caption);caption.SetSiblingIndex(2);
        var heading=Label(canvas,"Title",title,12,height/2-52,width-130,50,34,true);heading.color=Color.white;
        var icon=Box(canvas,"WindowIcon",-width/2+48,height/2-52,24,30);Fill(icon,White);Border(icon,true);
        for(int i=0;i<3;i++)Fill(Box(icon,"Line"+i,0,6-i*6,12,2),Ink);
    }
    public static string ApplyUpgrader()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        const string path="Assets/Base/BaseUpgrader/BaseUpgrader.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var canvas=root.transform.Find("Canvas");Frame(canvas,1000,700,"Base Upgrades");
            foreach(var t in canvas.GetComponentsInChildren<TMP_Text>(true))Type(t,t.fontSize);
            canvas.Find("Title").GetComponent<TMP_Text>().color=Color.white;canvas.Find("Title").GetComponent<TMP_Text>().fontStyle=FontStyles.Bold;
            Label(canvas,"Subtitle","Improve the base for the next expedition.",-130,225,650,48,25);
            var balance=Box(canvas,"BalanceField",350,219,210,64);Fill(balance,White);Border(balance,true);balance.SetSiblingIndex(3);
            var money=canvas.Find("MoneyCounter");Center(money,350,219,186,54);var mt=money.GetComponent<TMP_Text>();Type(mt,36);mt.alignment=TextAlignmentOptions.Center;
            var bl=canvas.Find("BalanceLabel");Center(bl,350,262,210,26);Type(bl.GetComponent<TMP_Text>(),21);bl.GetComponent<TMP_Text>().text="Team balance";bl.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;
            Label(canvas,"Hint","Esc  /  Leave terminal",0,-314,920,28,21);
            var mask=canvas.Find("Mask");Center(mask,0,-65,924,480);mask.GetComponent<ScrollRect>().enabled=false;mask.Find("Scrollbar").gameObject.SetActive(false);
            var area=mask.Find("ShopArea");Center(area,0,0,924,480);Fill(area,Color.clear);area.GetComponent<GridLayoutGroup>().enabled=false;
            string[] names={"Drone","Detection","BeamWeapon","Casino","Shield","Charger"};
            for(int i=0;i<names.Length;i++)
            {
                var card=area.Find(names[i]);Top(card,i%2*474,i/2*160,450,150);card.GetComponent<VerticalLayoutGroup>().enabled=false;
                Fill(card,White,true);Border(card,true);
                var name=card.Find("Name");Top(name,18,9,278,36);Type(name.GetComponent<TMP_Text>(),28,true);
                var price=card.Find("Price");Top(price,306,9,126,36);Type(price.GetComponent<TMP_Text>(),26);price.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineRight;
                var description=card.Find("Description");Top(description,18,48,414,56);Type(description.GetComponent<TMP_Text>(),24);
                var status=card.Find("Status");Top(status,18,110,250,30);Type(status.GetComponent<TMP_Text>(),21);
                var buy=card.Find("Buy").GetComponent<Button>();Top(buy.transform,290,104,142,38);ButtonStyle(buy);
                var label=buy.GetComponentInChildren<TMP_Text>();Center(label.transform,0,0,130,38);label.text="BUY";
                var indicator=card.Find("PurchasedIndicator");Top(indicator,7,7,5,136);Fill(indicator,Teal);
                ColorField(card.GetComponent<Base.BaseUpgrader.UpgradeOption>(),"errorHoverColor",new Color(.65f,.08f,.04f));
            }
            var upgrader=root.GetComponent<BaseUpgrader>();ColorField(upgrader,"availableColor",White);ColorField(upgrader,"purchasedColor",new Color32(210,235,230,255));
            ColorField(upgrader,"disabledColor",new Color32(174,177,181,255));ColorField(upgrader,"statusColor",Ink);ColorField(upgrader,"purchasedStatusColor",new Color(.03f,.28f,.18f));
            PrefabUtility.SaveAsPrefabAsset(root,path);return "Styled six BaseUpgrader cards, balance, statuses and controls.";
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static string ApplyWorldCard()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        const string path="Assets/Base/WorldChooser/WorldCard.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            Center(root.transform,0,0,360,388);Fill(root.transform,White);Border(root.transform,true);
            var name=root.transform.Find("Name");Top(name,22,24,316,68);Type(name.GetComponent<TMP_Text>(),32,true);name.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            Fill(Box(root.transform,"Rule",0,86,316,2),Color.gray);
            var enemies=root.transform.Find("Enemies");Top(enemies,22,126,316,44);Type(enemies.GetComponent<TMP_Text>(),28);enemies.GetComponent<TMP_Text>().text="Enemies: 1";enemies.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            var danger=root.transform.Find("Danger");Top(danger,22,176,316,44);Type(danger.GetComponent<TMP_Text>(),28);danger.GetComponent<TMP_Text>().text="Danger: 2.5";danger.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            var button=root.transform.Find("Buy").GetComponent<Button>();Top(button.transform,76,294,208,56);ButtonStyle(button);
            var label=button.GetComponentInChildren<TMP_Text>();Center(label.transform,0,0,192,56);label.text="CHOOSE";Type(label,26);
            var indicator=root.transform.Find("SelectedIndicator");Top(indicator,7,7,5,374);Fill(indicator,Teal);
            var card=root.GetComponent<Base.WorldChooser.WorldCard>();ColorField(card,"normalColor",White);ColorField(card,"selectedColor",new Color32(210,235,230,255));card.RefreshSelection(-1);
            PrefabUtility.SaveAsPrefabAsset(root,path);return "Styled generated WorldCard template and its selection state.";
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static string ApplyChooser()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        const string path="Assets/Base/WorldChooser/WorldChooser.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var canvas=root.transform.Find("Canvas");Frame(canvas,1200,800,"World Chooser");
            Label(canvas,"Subtitle","Choose a destination for the next expedition.",0,286,1128,36,26);
            var current=Box(canvas,"SelectedRealmField",0,216,1128,84);Fill(current,White);Border(current,true);current.SetSiblingIndex(3);
            Label(canvas,"CurrentRealmLabel","Selected realm",-416,216,250,52,26);
            var name=canvas.Find("Current Realm");Center(name,155,216,720,58);Type(name.GetComponent<TMP_Text>(),34,true);name.GetComponent<TMP_Text>().color=new Color(.6f,.08f,.04f);name.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            Label(canvas,"AvailableLabel","Available realms",0,145,1128,32,25,true);
            var area=canvas.Find("ShopArea");Center(area,0,-76,1128,388);Fill(area,Color.clear);
            var grid=area.GetComponent<GridLayoutGroup>();grid.cellSize=new Vector2(360,388);grid.spacing=new Vector2(24,0);grid.padding=new RectOffset();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.startCorner=GridLayoutGroup.Corner.UpperLeft;grid.startAxis=GridLayoutGroup.Axis.Horizontal;grid.childAlignment=TextAnchor.UpperLeft;
            var hint=Label(canvas,"Hint","Selection is shared with the team.   Esc / Leave terminal",0,-336,1128,42,23);hint.margin=new Vector4(12,0,12,0);Border(hint.transform,true);
            var chooser=root.GetComponent<Base.WorldChooser.WorldChooser>();ColorField(chooser,"noMissionColor",new Color(.6f,.08f,.04f));ColorField(chooser,"selectedMissionColor",new Color(.03f,.28f,.18f));
            PrefabUtility.SaveAsPrefabAsset(root,path);return "Styled WorldChooser frame, current realm and three generated destination cards.";
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static string UpdateSceneOverrides()
    {
        var root=GameObject.Find("/Base/Furniture/WorldChooser");if(root==null || root.scene.isDirty)throw new Exception("Saved Lobby scene required");
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            var s=new SerializedObject(text);foreach(var name in new[]{"m_fontColor32","m_fontColor"})
            {var p=s.FindProperty(name);if(p!=null && p.prefabOverride)PrefabUtility.RevertPropertyOverride(p,InteractionMode.AutomatedAction);}
        }
        var money=GameObject.Find("/Base/Furniture/BaseUpgrader/Canvas/MoneyCounter").GetComponent<TMP_Text>();
        var size=new SerializedObject(money).FindProperty("m_fontSize");if(size.prefabOverride)PrefabUtility.RevertPropertyOverride(size,InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);return "Updated only obsolete text color/size overrides on the two terminals.";
    }
}
