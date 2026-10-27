using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Player.UI;
using Player.Movement;

public static class PauseFadeChecks
{
    private const string Folder="Assets/Temporary/PauseFadeChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
    public static string Prepare()
    {
        Require(!Application.isPlaying && !AssetDatabase.IsValidFolder(Folder),"Fixture already active");
        SessionState.SetString("PauseFade.PreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        AssetDatabase.CreateFolder("Assets/Temporary","PauseFadeChecks");
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        EditorSceneManager.SaveScene(scene,Folder+"/Check.unity");EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Folder+"/Check.unity");
        return "Prepared isolated pause transition check.";
    }
    public static async Task<string> Run()
    {
        Require(Application.isPlaying && SceneManager.GetActiveScene().path==Folder+"/Check.unity","Wrong scene");
        var actor=new GameObject("Pause input check");var movement=actor.AddComponent<PlayerMovement>();movement.enabled=false;
        movement.controls=new Controls();movement.controls.System.Enable();movement.SwitchToGameplayControls();PlayerMovement.Instance=movement;
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/UI/PauseMenu.prefab"));
        var pause=root.GetComponent<PauseUIManager>();var group=root.GetComponent<CanvasGroup>();
        // Settings persistence is outside this isolated UI check; do not load or write player settings.
        pause.settingsMenu.GetComponent<Assets.Player.UI.SettingsMenu>().enabled=false;
        foreach(var binding in pause.settingsMenu.GetComponentsInChildren<Assets.Player.UI.InputBindings>(true))binding.enabled=false;
        var pauseInput=typeof(PauseUIManager).GetMethod("UpdateMenuState",Private);
        var movementInput=typeof(PlayerMovement).GetMethod("OnPause",Private);
        Action toggle=()=>{movementInput.Invoke(movement,new object[]{default(InputAction.CallbackContext)});pauseInput.Invoke(pause,new object[]{default(InputAction.CallbackContext)});};
        try
        {
            Application.runInBackground=true;
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
            await Task.Delay(500);
            Require(group.alpha==0 && !pause.pauseMenu.activeSelf && !group.blocksRaycasts,"Initial state visible");
            Require(root.GetComponentInChildren<ScreenShatterEffect>(true)==null,"Shatter effect remains");
            Time.timeScale=0;
            toggle();Require(movement.isPaused && group.alpha==0 && pause.pauseMenu.activeSelf && !group.interactable,"Opening flashed or input not paused");
            await Task.Delay(80);Require(group.alpha>0 && group.alpha<1,"No intermediate fade opacity");
            float partial=group.alpha;toggle();Require(Mathf.Approximately(group.alpha,partial) && !group.blocksRaycasts,"Close jumped opacity or retained input");
            toggle();Require(Mathf.Approximately(group.alpha,partial),"Reopen jumped opacity");
            await Task.Delay(400);Require(group.alpha==1 && group.interactable && movement.isPaused,"Unscaled fade did not complete");
            pause.OpenSettings();Require(pause.settingsMenu.activeSelf,"Settings did not open");
            toggle();Require(pause.settingsMenu.activeSelf && !group.interactable && !movement.isPaused,"Settings disappeared before fade");
            await Task.Delay(300);Require(group.alpha==0 && !pause.settingsMenu.activeSelf && !pause.pauseMenu.activeSelf,"Settings not hidden after fade");
            toggle();await Task.Delay(300);pause.Resume();
            Require(!movement.isPaused && movement.controls.Gameplay.enabled && !movement.controls.UI.enabled,"Resume did not restore controls");
            await Task.Delay(250);Require(group.alpha==0 && !pause.pauseMenu.activeSelf,"Resume did not finish fade");
            for(int i=0;i<6;i++)toggle();await Task.Delay(300);Require(group.alpha==0 && !pause.pauseMenu.activeSelf && !movement.isPaused,"Rapid toggles left stale animation");
            toggle();await Task.Delay(70);root.SetActive(false);Require(group.alpha==0 && !group.blocksRaycasts,"Disable did not clear transition");
            root.SetActive(true);movement.isPaused=false;toggle();await Task.Delay(300);Require(group.alpha==1,"Reenabled menu failed to open");
            pause.Resume();await Task.Delay(250);
            return "PASS: intermediate opacity, zero-timescale fade, rapid reversal, Settings fade-out, Resume input restoration, disable/reopen, and no shatter component.";
        }
        finally
        {
            Time.timeScale=1;UnityEngine.Object.Destroy(root);PlayerMovement.Instance=null;movement.controls.Dispose();movement.controls=null;UnityEngine.Object.Destroy(actor);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
    }
    public static string Restore()
    {
        Require(!Application.isPlaying,"Stop Play Mode first");
        var previous=SessionState.GetString("PauseFade.PreviousStart","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        AssetDatabase.DeleteAsset(Folder);SessionState.EraseString("PauseFade.PreviousStart");return "Restored Editor and removed temporary scene.";
    }
}
