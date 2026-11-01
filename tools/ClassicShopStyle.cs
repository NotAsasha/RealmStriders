using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;


public static class ClassicShopStyle
{
    private const string PrefabPath = "Assets/Base/Shop/Shop.prefab";

    private static readonly Color Face = new Color32(192, 195, 199, 255);
    private static readonly Color Shadow = new Color32(128, 128, 128, 255);
    private static readonly Color Dark = new Color32(32, 32, 32, 255);
    private static readonly Color Teal = new Color32(0, 128, 128, 255);

    private static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>();
    }

    private static TMP_FontAsset Font()
    {
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    }
    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var r = parent.Find(name) as RectTransform;
        if (r == null) { r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); }
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition3D = new Vector3(x, y, -.1f); r.sizeDelta = new Vector2(width, height);
        r.localRotation = Quaternion.identity; r.localScale = Vector3.one;
        return r;
    }

    private static Image Fill(Transform t, Color color)
    {
        var image = Ensure<Image>(t.gameObject); image.color = color; image.sprite = null;
        image.raycastTarget = false; return image;
    }

    private static void Edge(Transform t, string name, float x, float y, float width, float height, Color color)
    {
        Fill(Rect(t, name, x, y, width, height), color);
    }

    private static void Border(Transform t, bool inset)
    {
        var r = (RectTransform)t; float w = r.rect.width, h = r.rect.height;
        Color top = inset ? Shadow : Color.white, bottom = inset ? Color.white : Dark;
        Color innerTop = inset ? Dark : Face, innerBottom = inset ? Face : Shadow;
        Edge(t, "BevelTop", 0, h / 2 - 1.5f, w, 3, top);
        Edge(t, "BevelLeft", -w / 2 + 1.5f, 0, 3, h, top);
        Edge(t, "BevelBottom", 0, -h / 2 + 1.5f, w, 3, bottom);
        Edge(t, "BevelRight", w / 2 - 1.5f, 0, 3, h, bottom);
        Edge(t, "InnerTop", 0, h / 2 - 4.5f, w - 6, 3, innerTop);
        Edge(t, "InnerLeft", -w / 2 + 4.5f, 0, 3, h - 6, innerTop);
        Edge(t, "InnerBottom", 0, -h / 2 + 4.5f, w - 6, 3, innerBottom);
        Edge(t, "InnerRight", w / 2 - 4.5f, 0, 3, h - 6, innerBottom);
    }

    private static TMP_Text Label(Transform parent, string name, string content, float x, float y, float width, float height, float size, TMP_FontAsset font)
    {
        var t = Ensure<TextMeshProUGUI>(Rect(parent, name, x, y, width, height).gameObject);
        t.font = font; t.text = content; t.fontSize = size; t.enableAutoSizing = false;
        t.fontStyle = FontStyles.Normal; t.color = Color.black; t.raycastTarget = false;
        t.alignment = TextAlignmentOptions.MidlineLeft; return t;
    }

    public static string Apply()
    {
        if (Application.isPlaying) throw new Exception("Exit Play Mode before styling");
        var font = Font();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var canvas = root.transform.Find("Canvas");
            canvas.localPosition = new Vector3(.0095f, 1.425f, 0);
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                t.font = font; t.fontSharedMaterial = font.material; t.fontStyle = FontStyles.Normal; t.color = Color.black;
                t.fontSizeMax = t.fontSize; t.fontSizeMin = Mathf.Min(t.fontSize, 20);
                t.characterSpacing = 0;
            }
            var backdrop = canvas.Find("MenuBackdrop"); Fill(backdrop, Teal);
            foreach (Transform child in backdrop) child.gameObject.SetActive(false);
            var frame = Rect(canvas, "ClassicWindow", 0, 0, 868, 648);
            Fill(frame, Face); Border(frame, false); frame.SetSiblingIndex(1);
            var bar = Rect(canvas, "TitleBar", 0, 291, 844, 44); Fill(bar, new Color32(128, 132, 138, 255));
            var title = (RectTransform)canvas.Find("Title");
            title.anchorMin = title.anchorMax = title.pivot = new Vector2(.5f, .5f);
            title.anchoredPosition3D = new Vector3(20, 291, -.2f); title.sizeDelta = new Vector2(776, 44);
            var tt = title.GetComponent<TMP_Text>(); tt.text = "Equipment Shop"; tt.fontSize = 30;
            tt.enableAutoSizing = false; tt.fontStyle = FontStyles.Bold; tt.color = Color.white;
            var icon = Rect(canvas, "WindowIcon", -401, 291, 24, 30); Fill(icon, Color.white); Border(icon, true);
            for (int i = 0; i < 3; i++) Edge(icon, "Line" + i, 0, 6 - 6 * i, 12, 2, Dark);
            var location = canvas.Find("Location").GetComponent<TMP_Text>(); location.fontSize = 24; location.enableAutoSizing = false;
            var catalog = canvas.Find("Mask/ShopArea");
            foreach (var img in catalog.GetComponentsInChildren<Image>(true))
            {
                if (img.GetComponent<Button>() != null) continue;
                img.color = img.name == "Accent" || img.name == "Rule" ? Shadow : Face;
            }
            var cards = root.GetComponentsInChildren<Base.Shop.ItemCard>(true);
            foreach (var card in cards) { Fill(card.transform, Face); Border(card.transform, true); }
            foreach (var b in catalog.GetComponentsInChildren<Button>(true).Concat(cards.SelectMany(c => c.GetComponentsInChildren<Button>(true))))
            {
                bool tile = b.transform.parent.name == "Items";
                var img = b.GetComponent<Image>(); img.color = Color.white; img.raycastTarget = true;
                var cb = b.colors; cb.normalColor = tile ? new Color32(245, 245, 245, 255) : Face;
                cb.highlightedColor = tile ? Color.white : new Color32(210, 212, 215, 255);
                cb.pressedColor = new Color32(160, 163, 168, 255); cb.selectedColor = cb.normalColor;
                cb.disabledColor = new Color32(174, 177, 181, 255); cb.fadeDuration = 0; b.colors = cb;
                Border(b.transform, tile);
                if (!tile)
                {
                    var label = b.GetComponentInChildren<TMP_Text>();
                    label.text = b.name == "Back" ? "< Back" : "Buy"; label.fontSize = 26;
                    label.alignment = TextAlignmentOptions.Center;
                }
            }
            foreach (var card in cards)
            {
                card.transform.Find("Name").GetComponent<TMP_Text>().fontSize = 30;
                card.transform.Find("Description").GetComponent<TMP_Text>().fontSize = 24;
                var preview = card.transform.Find("ItemPreview"); Border(preview, true);
            }
            var status = Label(canvas, "StatusBar", "Select an item to view its details.", 0, -268, 820, 38, 22, font);
            status.gameObject.SetActive(true); status.margin = new Vector4(12, 0, 8, 0); Border(status.transform, true);
            var balance = root.transform.Find("HardwareControls/BalanceDisplay"); Fill(balance, Face); Border(balance, false);
            var valueField = Rect(balance, "ValueField", 0, -23, 326, 90); Fill(valueField, Color.white); Border(valueField, true); valueField.SetAsFirstSibling();
            var number = balance.Find("MoneyCounter").GetComponent<TMP_Text>(); number.fontSize = 52; number.fontSizeMax = 52; number.fontStyle = FontStyles.Normal;
            var caption = balance.Find("BalanceLabel").GetComponent<TMP_Text>(); caption.text = "Team balance"; caption.fontSize = 22; caption.fontSizeMax = 22;
            foreach (var b in root.GetComponentsInChildren<Base.Shop.ShopCategoryButton>(true))
            {
                var statusText = b.transform.Find("Requirement").GetComponent<TMP_Text>(); statusText.fontSize = 20; statusText.fontSizeMax = 20;
                var plate = Rect(b.transform, "StatusPlate", 0, -64, 106, 48);
                Fill(plate, Face); Border(plate, true); plate.SetAsFirstSibling();
                ConfigureCategory(b);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "Applied classic Windows 95/98 palette, smooth type, gray caption, raised buttons and inset fields to Shop.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureCategory(Base.Shop.ShopCategoryButton button)
    {
        var data = new SerializedObject(button);
        data.FindProperty("statusPlate").objectReferenceValue = button.transform.Find("StatusPlate").GetComponent<Image>();
        data.ApplyModifiedPropertiesWithoutUndo();
        bool available = data.FindProperty("requiredRating").intValue == 0;
        button.Refresh(available, available);
        EditorUtility.SetDirty(button);
    }

    public static string ApplyCategories()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (var button in root.GetComponentsInChildren<Base.Shop.ShopCategoryButton>(true))
                ConfigureCategory(button);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "Updated five category status plates and STAR captions.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static string Preview()
    {
        if (Application.isPlaying) throw new Exception("Preview expects Edit Mode");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        RenderTexture target = null; Texture2D pixels = null; GameObject rig = null;
        try
        {
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
            rig = new GameObject("Shop style preview"); SceneManager.MoveGameObjectToScene(rig, root.scene);
            var camera = rig.AddComponent<Camera>(); camera.scene = root.scene;
            var point = root.transform.Find("CameraPoint"); rig.transform.SetPositionAndRotation(point.position, point.rotation);
            rig.transform.position -= rig.transform.forward * .28f;
            camera.nearClipPlane = .01f; camera.farClipPlane = 20; camera.fieldOfView = 60;
            camera.aspect = 1500f / 1100f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f,.12f,.15f);
            var lightObject = new GameObject("Preview light", typeof(Light)); lightObject.transform.SetParent(rig.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(35,-65,0); var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
            foreach (var c in root.GetComponentsInChildren<Canvas>(true)) c.worldCamera = camera;
            foreach (var card in root.GetComponentsInChildren<Base.Shop.ItemCard>(true)) card.gameObject.SetActive(false);
            var chooser = root.GetComponentInChildren<CategorySwitch>(true);
            for(int i=0;i<chooser.categories.Length;i++) chooser.categories[i].SetActive(i==0);
            root.transform.Find("Canvas/Mask/ShopArea").gameObject.SetActive(true);
            int[] thresholds={0,4,7,10,13};
            for(int i=0;i<5;i++)root.GetComponentsInChildren<Base.Shop.ShopCategoryButton>(true)[i].Refresh(i==0, thresholds[i]<=4);
            root.transform.Find("HardwareControls/BalanceDisplay/MoneyCounter").GetComponent<TMP_Text>().text="320";
            root.transform.Find("Canvas/Location").GetComponent<TMP_Text>().text="Team rating: 4     |     Category 1 / 5";
            target = new RenderTexture(1500,1100,24); target.Create(); camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            foreach(var text in root.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
            camera.Render(); camera.Render();
            var previous=RenderTexture.active; RenderTexture.active=target;
            pixels=new Texture2D(1500,1100,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1500,1100),0,0);pixels.Apply();RenderTexture.active=previous;
            File.WriteAllBytes("tools/shop-classic-preview.png",pixels.EncodeToPNG());
            return "Rendered isolated Shop preview without entering Play Mode or registering network prefabs.";
        }
        finally
        {
            if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}

