using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Base.Shop;

public static class UpdatePauseAndShop
{
    private static readonly Color Text = new(.922f,.914f,.878f);
    private static readonly Color Muted = new(.60f,.61f,.63f);
    private static readonly Color Panel = new(.043f,.043f,.055f, .97f);
    private static readonly Color Surface = new(.12f,.12f,.14f);
    private static readonly Color Hover = new(.24f,.25f,.28f);
    private static TMP_FontAsset title, button, body, detail;
    private static void Fonts()
    {
        title=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/CinzelDecorative-Bold SDF.asset");
        button=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Syncopate-Bold SDF.asset");
        body=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/SmoochSans-ExtraBold SDF.asset");
        detail=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/UnicaOne-Regular SDF.asset");
    }
    private static void Backup(string path)
    {
        Directory.CreateDirectory("tools/pause-shop-backup");
        string destination="tools/pause-shop-backup/"+path.Replace('/','_')+".backup";
        if(!File.Exists(destination)) File.Copy(path,destination);
    }
    private static void Edit(string path,Action<GameObject> action)
    {
        Backup(path);
        var root=PrefabUtility.LoadPrefabContents(path);
        try { action(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static RectTransform Box(Transform parent,string name)
    {
        var found=parent.Find(name);
        if(found!=null)return (RectTransform)found;
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);return rect;
    }
    private static void Place(Transform transform,float x,float y,float w,float h)
    {
        var rect=(RectTransform)transform;
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
        rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
        rect.localPosition=new Vector3(rect.localPosition.x,rect.localPosition.y,0);
    }
    private static void Stretch(Transform transform)
    {
        var r=(RectTransform)transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;
        r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
        r.localPosition=new Vector3(r.localPosition.x,r.localPosition.y,0);
    }
    private static Image Fill(Transform t,Color color,bool raycast=false)
    {
        var image=t.GetComponent<Image>()??t.gameObject.AddComponent<Image>();
        image.sprite=null;image.material=null;image.type=Image.Type.Simple;image.color=color;image.raycastTarget=raycast;return image;
    }
    private static TMP_Text Label(Transform parent,string name,string value,float x,float y,float w,float h,TMP_FontAsset font,float size,Color color)
    {
        var rect=Box(parent,name);Place(rect,x,y,w,h);
        var t=rect.GetComponent<TMP_Text>()??rect.gameObject.AddComponent<TextMeshProUGUI>();
        t.text=value;t.font=font;t.color=color;t.fontSize=size;t.fontSizeMax=size;t.fontSizeMin=size*.75f;
        t.enableAutoSizing=true;t.fontStyle=FontStyles.Normal;t.alignment=TextAlignmentOptions.MidlineLeft;
        t.raycastTarget=false;t.margin=Vector4.zero;return t;
    }
    private static void StyleButton(Button b,float x,float y,float w,float h,string value,float size=26)
    {
        Place(b.transform,x,y,w,h);var image=Fill(b.transform,Color.white,true);
        b.targetGraphic=image;b.transition=Selectable.Transition.ColorTint;
        var colors=b.colors;colors.normalColor=Surface;colors.highlightedColor=Hover;
        colors.selectedColor=Hover;colors.pressedColor=new Color(.18f,.19f,.21f);
        colors.disabledColor=new Color(.075f,.075f,.09f);colors.fadeDuration=.12f;colors.colorMultiplier=1;b.colors=colors;
        var label=b.GetComponentInChildren<TMP_Text>(true);
        string labelName=label==null?"Label":label.name;
        Label(b.transform,labelName,value,16,0,w-32,h,button,size,Text);
    }
    private static void Ref(UnityEngine.Object target,string name,UnityEngine.Object value)
    {
        var so=new SerializedObject(target);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Refs(UnityEngine.Object target,string name,UnityEngine.Object[] values)
    {
        var so=new SerializedObject(target);var p=so.FindProperty(name);p.arraySize=values.Length;
        for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Int(UnityEngine.Object target,string name,int value)
    {
        var so=new SerializedObject(target);so.FindProperty(name).intValue=value;so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Pause(GameObject root)
    {
        var manager=root.GetComponent<Player.UI.PauseUIManager>();
        var settings=root.transform.Find("Settings").gameObject;
        PrefabUtility.RevertPrefabInstance(settings,InteractionMode.AutomatedAction);
        Stretch(settings.transform);
        var group=settings.GetComponent<CanvasGroup>();group.alpha=1;group.interactable=true;group.blocksRaycasts=true;
        var menu=settings.GetComponent<Assets.Player.UI.SettingsMenu>();
        menu.OnClose=new UnityEngine.Events.UnityEvent();UnityEventTools.AddPersistentListener(menu.OnClose,manager.CloseSettings);
        PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
        Ref(manager,"settingsMenu",settings);
        var background=root.transform.Find("Backbround");Stretch(background);Fill(background,Panel,true);
        var panel=background.Find("Panel");Stretch(panel);Fill(panel,Color.clear);
        Ref(manager,"pauseContent",panel.gameObject);
        Label(panel,"Text (TMP)","Paused",136,156,1000,90,title,72,Text);
        Label(panel,"Subtitle","Take a moment. Your crew is still out there.",136,256,1500,50,body,40,Muted);
        var resume=Box(panel,"Resume").GetComponent<Button>()??panel.Find("Resume").gameObject.AddComponent<Button>();
        StyleButton(resume,136,368,570,75,"RESUME",36);
        resume.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(resume.onClick,manager.Resume);
        var settingsButton=panel.Find("Settings").GetComponent<Button>();
        StyleButton(settingsButton,136,468,570,75,"SETTINGS",36);
        settingsButton.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(settingsButton.onClick,manager.OpenSettings);
        StyleButton(panel.Find("ExitToLobby").GetComponent<Button>(),136,896,660,75,"LEAVE LOBBY",32);
        Label(panel,"CrewLabel","CREW",930,336,850,60,button,32,Text);
        var parent=panel.Find("Parent");Place(parent,930,414,850,414);
        var grid=parent.GetComponent<GridLayoutGroup>();grid.cellSize=new Vector2(850,126);grid.spacing=new Vector2(0,16);
        grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=1;
        root.GetComponent<CanvasScaler>().screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        settings.SetActive(false);background.gameObject.SetActive(false);
    }

    private static void PlayerCard(GameObject root)
    {
        ((RectTransform)root.transform).sizeDelta=new Vector2(850,126);Fill(root.transform,Surface);
        var card=root.GetComponent<Player.UI.PlayerCard>();Place(card.playerImage.transform,16,16,90,90);
        var name=card.playerName;Place(name.transform,128,8,530,48);name.font=detail;name.color=Text;
        name.enableAutoSizing=true;name.fontSizeMax=34;name.fontSizeMin=24;
        var volume=root.transform.Find("VolumeSliderContainer");Place(volume,128,62,530,48);
        var slider=volume.GetComponentInChildren<Slider>(true);Place(slider.transform,145,8,370,32);
        var label=volume.GetComponentInChildren<TMP_Text>(true);Place(label.transform,0,0,135,48);label.font=body;label.fontSize=28;label.color=Muted;
        foreach(var image in slider.GetComponentsInChildren<Image>(true))image.color=image.name=="Background"?Hover:Text;
        StyleButton(root.GetComponentInChildren<Button>(true),686,34,144,58,"KICK",21);
    }

    private static GameObject BakeModel(ItemCard card)
    {
        if(!AssetDatabase.IsValidFolder("Assets/Base/Shop/Previews"))AssetDatabase.CreateFolder("Assets/Base/Shop","Previews");
        var root=new GameObject(card.itemName+" Preview");
        var model=new GameObject("Mesh");model.transform.SetParent(root.transform,false);
        var source=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(card.itemPrefab.gameObject));
        try
        {
            foreach(var shape in source.GetComponentsInChildren<UnityEngine.ProBuilder.ProBuilderMesh>(true))
            {
                shape.ToMesh();shape.Refresh();
                var filter=shape.GetComponent<MeshFilter>();
                string path="Assets/Base/Shop/Previews/"+card.itemName+"Mesh.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null){mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);AssetDatabase.CreateAsset(mesh,path);}
                else EditorUtility.CopySerialized(filter.sharedMesh,mesh);
                filter.sharedMesh=mesh;
            }
            foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.enabled)continue;
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                var part=new GameObject(renderer.name,typeof(MeshFilter),typeof(MeshRenderer));part.layer=31;
                part.transform.SetParent(model.transform,false);
                Matrix4x4 matrix=source.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                part.transform.localPosition=matrix.GetColumn(3);part.transform.localRotation=matrix.rotation;
                part.transform.localScale=matrix.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var target=part.GetComponent<MeshRenderer>();target.sharedMaterials=renderer.sharedMaterials;
                target.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;target.receiveShadows=false;
                target.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;
                target.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            var renderers=model.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length==0)throw new Exception("No preview mesh: "+card.itemName);
            Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float scale=1.85f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            model.transform.localScale=Vector3.one*scale;model.transform.localPosition=-bounds.center*scale;
            return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Base/Shop/Previews/"+card.itemName+".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); PrefabUtility.UnloadPrefabContents(source); }
    }

    private static void Shop(GameObject root)
    {
        var shop=root.GetComponent<Base.Shop.Shop>();var canvas=root.transform.Find("Canvas");
        var backdrop=Box(canvas,"MenuBackdrop");Stretch(backdrop);Fill(backdrop,Panel,true);backdrop.SetAsFirstSibling();
        var heading=Label(canvas,"Title","Equipment",38,24,530,78,title,56,Text);
        Label(canvas,"BalanceLabel","TEAM CREDITS",640,28,220,28,detail,22,Muted);
        var money=canvas.Find("MoneyCounter").GetComponent<TMP_Text>();Place(money.transform,640,54,220,48);
        money.font=detail;money.fontSize=38;money.color=Text;money.alignment=TextAlignmentOptions.MidlineLeft;
        Label(canvas,"Hint","ESC  /  LEAVE TERMINAL",38,666,824,26,detail,20,Muted);
        var tabs=canvas.Find("Buttons");Place(tabs,38,122,824,76);tabs.GetComponent<HorizontalLayoutGroup>().enabled=false;
        var categories=tabs.GetComponent<CategorySwitch>();
        var mask=canvas.Find("Mask");Place(mask,38,218,824,432);
        var catalog=mask.Find("ShopArea");Stretch(catalog);
        var preview=canvas.GetComponent<ShopItemPreview>()??canvas.gameObject.AddComponent<ShopItemPreview>();
        Ref(preview,"shopCanvas",canvas.GetComponent<Canvas>());
        var cards=root.GetComponentsInChildren<ItemCard>(true);
        var pages=categories.categories.ToList();
        for(int i=pages.Count;i<5;i++)pages.Add(Box(catalog,"StarZone "+(i+1)).gameObject);
        categories.categories=pages.ToArray();
        var indicators=new Image[5];var blockers=new CategoryBlocker[5];
        int[] ratings={0,4,7,10,13};
        for(int i=0;i<5;i++)
        {
            var b=categories.buttonComponents[i];
            StyleButton(b,i*168,0,152,76,(i+1)+ (i==0?" STAR":" STARS"),21);
            var text=b.GetComponentInChildren<TMP_Text>();Place(text.transform,12,4,128,36);
            Label(b.transform,"Requirement",i==0?"AVAILABLE":"RATING "+ratings[i],12,39,128,26,detail,20,Muted);
            var rule=Box(b.transform,"Selected");Place(rule,12,72,128,3);indicators[i]=Fill(rule,new Color(.55f,.56f,.59f));indicators[i].enabled=i==0;
            blockers[i]=b.GetComponent<CategoryBlocker>();Int(blockers[i],"requiredRating",ratings[i]);
            Stretch(pages[i].transform);Fill(pages[i].transform,Color.clear);
            var items=pages[i].transform.Find("Items");
            if(items!=null)
            {
                Place(items,0,0,824,432);var grid=items.GetComponent<GridLayoutGroup>();grid.enabled=false;
                for(int j=0;j<items.childCount;j++)
                {
                    var entry=items.GetChild(j).GetComponent<Button>();
                    var card=Enumerable.Range(0,entry.onClick.GetPersistentEventCount())
                        .Select(k=>entry.onClick.GetPersistentTarget(k)).OfType<GameObject>()
                        .Select(g=>g.GetComponent<ItemCard>()).FirstOrDefault(c=>c!=null);
                    if(card==null)throw new Exception("Missing item card for "+entry.name);
                    Int(card,"requiredRating",ratings[i]);
                    string itemLabel=entry.GetComponentInChildren<TMP_Text>().text;
                    StyleButton(entry,j%2*424,j/2*208,400,184,itemLabel,27);
                    var name=entry.GetComponentInChildren<TMP_Text>();Place(name.transform,24,24,352,75);
                    Label(entry.transform,"Price",card.Price+" CREDITS",24,126,352,34,detail,28,Muted);
                }
            }
            else
            {
                Label(pages[i].transform,"EmptyTitle","NO EQUIPMENT AVAILABLE",32,110,760,68,button,28,Text);
                Label(pages[i].transform,"EmptyDetail","Check another category.",32,190,760,55,body,32,Muted);
            }
            pages[i].SetActive(i==0);
        }
        Refs(categories,"blockers",blockers);Refs(categories,"indicators",indicators);
        Ref(categories,"catalog",catalog.gameObject);Refs(categories,"itemCards",cards);
        foreach(var card in cards)
        {
            Stretch(card.transform);Fill(card.transform,Panel,true);
            var raw=Box(card.transform,"ItemPreview");Place(raw,400,20,414,392);raw.SetAsFirstSibling();
            var image=raw.GetComponent<RawImage>()??raw.gameObject.AddComponent<RawImage>();image.raycastTarget=false;
            var name=card.transform.Find("Name").GetComponent<TMP_Text>();
            Label(card.transform,"Name",name.text,18,8,364,72,button,32,Text);
            var description=card.transform.Find("Description").GetComponent<TMP_Text>();
            Label(card.transform,"Description",description.text,18,142,364,184,body,30,Muted).alignment=TextAlignmentOptions.TopLeft;
            Label(card.transform,"Price",card.Price+" CREDITS",18,90,364,46,detail,34,Text);
            StyleButton(card.transform.Find("Buy").GetComponent<Button>(),18,352,176,64,"BUY",25);
            StyleButton(card.transform.Find("Back").GetComponent<Button>(),214,352,168,64,"< BACK",23);
            Ref(card,"shop",shop);Ref(card,"preview",preview);Ref(card,"previewImage",image);
            Ref(card,"catalog",catalog.gameObject);
            Ref(card,"previewModel",BakeModel(card));Ref(card,"buyButton",card.transform.Find("Buy").GetComponent<Button>());
            card.gameObject.SetActive(false);
        }
        // All controls share the original screen plane, rather than separate floating depths.
        foreach(Transform child in canvas)child.localPosition=new Vector3(child.localPosition.x,child.localPosition.y,-200f);
        Fill(backdrop,new Color(Panel.r,Panel.g,Panel.b,1f),true);
        var cameraPoint=root.transform.Find("CameraPoint");
        cameraPoint.localPosition=new Vector3(-1.03f,1.576f,0);
        cameraPoint.localRotation=Quaternion.Euler(0,90,0);
        catalog.gameObject.SetActive(true);
    }

    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Exit Play Mode first.");Fonts();
        Edit("Assets/Player/UI/PauseMenu.prefab",Pause);
        Edit("Assets/Player/UI/Card.prefab",PlayerCard);
        Edit("Assets/Player/Player.prefab",root=>
        {
            var pause=root.GetComponentInChildren<Player.UI.PauseUIManager>(true);
            var mods=PrefabUtility.GetPropertyModifications(pause.gameObject);
            PrefabUtility.SetPropertyModifications(pause.gameObject,mods.Where(m=>m.target!=null && !(m.target is RectTransform)).ToArray());
        });
        Edit("Assets/Base/Shop/Shop.prefab",Shop);
        AssetDatabase.SaveAssets();
        return "Updated Pause and player overrides, crew cards, five Shop pages with 0/4/7/10/13 unlocks, and eleven mesh-only item previews.";
    }

    public static string RebuildPreviews()
    {
        if(Application.isPlaying)throw new Exception("Exit Play Mode first.");
        Edit("Assets/Base/Shop/Shop.prefab",root=>
        {
            foreach(var card in root.GetComponentsInChildren<ItemCard>(true))
                Ref(card,"previewModel",BakeModel(card));
        });
        return "Rebuilt Shop mesh previews without changing layout or item data.";
    }
}
