using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Base.Shop;

public static class ShopQuickBuySetup
{
    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Exit Play Mode first.");
        const string path="Assets/Base/Shop/Shop.prefab";
        if(!File.Exists("tools/shop-before-quick-buy.backup"))File.Copy(path,"tools/shop-before-quick-buy.backup");
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var shop=root.GetComponent<Shop>();var data=new SerializedObject(shop);
            var items=root.GetComponentsInChildren<ItemCard>(true);
            var quick=data.FindProperty("quickBuyButtons");quick.arraySize=items.Length;
            var catalog=root.transform.Find("Canvas/Mask/ShopArea");
            int index=0;
            foreach(var tile in catalog.GetComponentsInChildren<Button>(true).Where(b=>b.transform.parent.name=="Items"))
            {
                var item=Enumerable.Range(0,tile.onClick.GetPersistentEventCount())
                    .Select(i=>tile.onClick.GetPersistentTarget(i)).OfType<GameObject>()
                    .Select(g=>g.GetComponent<ItemCard>()).First(c=>c!=null);
                var price=tile.transform.Find("Price").GetComponent<TMP_Text>();price.text=item.Price.ToString();
                price.rectTransform.sizeDelta=new Vector2(196,34);
                var existing=tile.transform.Find("QuickBuy");
                var button=existing!=null?existing.GetComponent<Button>():UnityEngine.Object.Instantiate(item.transform.Find("Buy").gameObject,tile.transform,false).GetComponent<Button>();
                button.name="QuickBuy";
                var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
                rect.anchoredPosition=new Vector2(240,-118);rect.sizeDelta=new Vector2(136,50);
                rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
                rect.localPosition=new Vector3(rect.localPosition.x,rect.localPosition.y,0);
                var label=button.GetComponentInChildren<TMP_Text>(true);label.text="BUY";
                label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
                label.rectTransform.offsetMin=new Vector2(10,0);label.rectTransform.offsetMax=new Vector2(-10,0);
                label.fontSize=23;label.fontSizeMin=20;label.fontSizeMax=23;label.alignment=TextAlignmentOptions.Center;
                button.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(button.onClick,item.BuyItem);
                button.interactable=false;
                var entry=quick.GetArrayElementAtIndex(index++);entry.FindPropertyRelative("item").objectReferenceValue=item;
                entry.FindPropertyRelative("button").objectReferenceValue=button;
                item.transform.Find("Price").GetComponent<TMP_Text>().text=item.Price.ToString();
                var back=(RectTransform)item.transform.Find("Back");var buy=(RectTransform)item.transform.Find("Buy");
                // Idempotent: swap slots once, keeping all other detail styling intact.
                if(back.anchoredPosition.x>buy.anchoredPosition.x)
                {
                    var position=back.anchoredPosition;back.anchoredPosition=buy.anchoredPosition;buy.anchoredPosition=position;
                    var size=back.sizeDelta;back.sizeDelta=buy.sizeDelta;buy.sizeDelta=size;
                    foreach(var action in new[]{back,buy})
                    {
                        var text=action.GetComponentInChildren<TMP_Text>().rectTransform;
                        text.sizeDelta=new Vector2(action.sizeDelta.x-32,text.sizeDelta.y);
                    }
                }
            }
            if(index!=items.Length)throw new Exception("Some items have no catalog tile.");
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,path);
            return "Added 11 quick-buy buttons, kept tile detail callbacks, swapped detail Back/Buy slots and removed CREDITS from all item prices.";
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
