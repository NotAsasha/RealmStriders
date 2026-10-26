using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Steam;

public static class MainMenuPreview
{
    public static string Verify()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var target=new RenderTexture(1280,720,24);
        var previous=RenderTexture.active;
        Camera camera=null;
        try
        {
            var source=GameObject.Find("MenuCamera/LobbyScreen");
            var root=UnityEngine.Object.Instantiate(source);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var cameraObject=new GameObject("Menu preview",typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,scene);
            camera=cameraObject.GetComponent<Camera>();
            var original=GameObject.Find("MenuCamera").GetComponent<Camera>();
            camera.CopyFrom(original);
            camera.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);
            camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene) |
                EditorSceneManager.GetSceneCullingMask(original.gameObject.scene);
            camera.targetTexture=target;
            var canvas=root.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
            var menu=root.transform.Find("Menu");
            var manager=menu.GetComponent<LobbyUiManager>();
            var settingsMenu=menu.Find("Settings").GetComponent<Assets.Player.UI.SettingsMenu>();
            for(int i=0;i<settingsMenu.OnClose.GetPersistentEventCount();i++)
                settingsMenu.OnClose.SetPersistentListenerState(i,UnityEventCallState.EditorAndRuntime);
            var settings=menu.Find("Settings").GetComponent<Canvas>();
            if(settings!=null) {settings.renderMode=RenderMode.ScreenSpaceCamera;settings.worldCamera=camera;settings.planeDistance=.9f;}
            string[] names={"main","join","host","settings","files","delete-confirmation","settings-sound","settings-controls"};
            for(int n=0;n<8;n++)
            {
                manager.ShowMainMenu();
                if(n>0)
                {
                    string path=n==1?"Base/JoinLobby":n==2||n==4||n==5?"Base/HostLobby":"Base/ToSettings";
                    Click(menu.Find(path).GetComponent<Button>());
                }
                string expected=n==0?"Base":n==1?"JoinLobby":n==2||n==4||n==5?"SaveSelector":"Settings";
                if(n==2) manager.SelectSave(1);
                if(n==4||n==5) manager.OpenFileManagement();
                if(n==5) manager.RequestDeleteFiles();
                if(n>=6) settingsMenu.SelectCategory(n-5);
                foreach(string page in new[]{"Base","JoinLobby","SaveSelector","Settings"})
                    if(menu.Find(page).gameObject.activeSelf!=(page==expected)) throw new Exception("Page navigation failed: "+expected);
                Canvas.ForceUpdateCanvases();
                foreach(var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                // Composite the overlay UI after world depth, matching ScreenSpaceOverlay.
                camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(original.gameObject.scene);
                camera.clearFlags=original.clearFlags;
                camera.Render();
                camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags=CameraClearFlags.Depth;
                camera.Render(); RenderTexture.active=target;
                var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                File.WriteAllBytes("tools/menu-"+names[n]+".png",image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                if(n>0)
                {
                    if(n==4||n==5) manager.CloseFileManagement();
                    string back=n==1?"JoinLobby/Back":n==2||n==4||n==5?"SaveSelector/Footer/Back":"Settings/Footer/BackToMenu";
                    Click(menu.Find(back).GetComponent<Button>());
                    if(!menu.Find("Base").gameObject.activeSelf || menu.Find(expected).gameObject.activeSelf)
                        throw new Exception("Back failed: "+expected);
                }
            }
            var exit=menu.Find("Base/Exit").GetComponent<Button>();
            if(exit.onClick.GetPersistentMethodName(0)!="ExitGame" || exit.onClick.GetPersistentTarget(0)!=manager)
                throw new Exception("Exit is not wired.");
            if(menu.Find("JoinLobby/Refresh").GetComponent<Button>().interactable) throw new Exception("Placeholder refresh should be disabled.");
            var create=menu.Find("SaveSelector/Footer/Create Lobby").GetComponent<Button>();
            bool host=false;
            for(int i=0;i<create.onClick.GetPersistentEventCount();i++)
                host|=create.onClick.GetPersistentMethodName(i)=="CreateLobbyButton";
            if(!host) throw new Exception("Host callback lost.");
            return "PASS: main/host/join/settings click routing and back, exclusive pages, Exit callback, disabled lobby browser, existing host action. Previews saved.";
        }
        finally
        {
            if(camera!=null)camera.targetTexture=null;
            RenderTexture.active=previous;
            EditorSceneManager.ClosePreviewScene(scene);
            target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void Click(Button button)
    {
        for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
        {
            // Avoid sounds and disk/network actions in this isolated UI check.
            bool navigation=button.onClick.GetPersistentTarget(i) is LobbyUiManager ||
                button.onClick.GetPersistentMethodName(i)=="Close";
            button.onClick.SetPersistentListenerState(i,navigation?UnityEventCallState.EditorAndRuntime:UnityEventCallState.Off);
        }
        button.onClick.Invoke();
    }
}
