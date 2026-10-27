using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class FinishPauseShop
{
    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Exit Play Mode first.");
        var root=PrefabUtility.LoadPrefabContents("Assets/Player/UI/PauseMenu.prefab");
        try
        {
            var settings=root.transform.Find("Settings");
            var sprite=settings.GetComponentsInChildren<TMP_Dropdown>(true)[0].template.Find("Viewport/Content/Item/Item Checkmark").GetComponent<Image>().sprite;
            var check=settings.Find("Graphics/DropDowns/VSync/Background/Checkmark").GetComponent<Image>();
            check.sprite=sprite;check.color=new Color(.78f,.79f,.81f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(check);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Player/UI/PauseMenu.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        string path="Assets/Scenes/Lobby/Lobby.unity";
        string backup="tools/pause-shop-backup/Lobby.unity.backup";
        if(!File.Exists(backup))File.Copy(path,backup);
        var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            foreach(var shop in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Base.Shop.Shop>(true)))
            {
                var modifications=PrefabUtility.GetPropertyModifications(shop.gameObject);
                var source=PrefabUtility.GetCorrespondingObjectFromSource(shop).transform;
                var canvas=source.Find("Canvas");var cameraPoint=source.Find("CameraPoint");
                var kept=modifications.Where(m=>
                {
                    if(m.target is RectTransform rect && rect!=canvas && rect.IsChildOf(canvas))return false;
                    if(m.target==cameraPoint)return false;
                    return true;
                }).ToArray();
                PrefabUtility.SetPropertyModifications(shop.gameObject,kept);
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        finally{EditorSceneManager.CloseScene(scene,true);}
        return "Removed obsolete Shop layout overrides in Lobby; restored neutral VSync check in Pause.";
    }
}
