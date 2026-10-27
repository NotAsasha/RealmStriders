using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Game.UI;

public static class UIAudioChecks
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
    public static async Task<string> Run()
    {
        Require(Application.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path=="Assets/Temporary/PauseFadeChecks/Check.unity","Use isolated scene");
        Application.runInBackground=true;EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
        AudioListener.pause=false;Time.timeScale=1;
        new GameObject("Audio test listener",typeof(AudioListener));
        var events=new GameObject("Audio test events",typeof(EventSystem)).GetComponent<EventSystem>();
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Base/WorldChooser/WorldCard.prefab"));
        var button=root.GetComponentInChildren<Button>();var sound=button.GetComponent<UIControlSound>();
        for(int i=button.onClick.GetPersistentEventCount()-1;i>0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);
        var pointer=new PointerEventData(events){button=PointerEventData.InputButton.Left,pointerEnter=button.gameObject};
        await Task.Delay(300);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
        Require(UnityEngine.Object.FindFirstObjectByType<UIAudio>()==null,"Hover created an audio player");
        UIAudio.PlayClick();
        var audio=UnityEngine.Object.FindFirstObjectByType<UIAudio>();Require(audio!=null,"Click did not create shared player");
        var hover=(AudioSource)typeof(UIAudio).GetField("hoverSource",Private).GetValue(audio);
        var click=(AudioSource)typeof(UIAudio).GetField("clickSource",Private).GetValue(audio);
        Require(!hover.isPlaying,"Hover should be silent");click.Stop();
        button.interactable=false;ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerEnterHandler);ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        Require(!hover.isPlaying && !click.isPlaying,"Disabled control played audio");button.interactable=true;
        pointer.button=PointerEventData.InputButton.Right;ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);Require(!click.isPlaying,"Right click played audio");
        pointer.button=PointerEventData.InputButton.Left;button.onClick.AddListener(()=>root.SetActive(false));
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        Require(!root.activeSelf && click.isPlaying && audio.gameObject.activeInHierarchy,"Navigation cut off click");click.Stop();root.SetActive(true);
        Time.timeScale=0;AudioListener.pause=true;
        ExecuteEvents.Execute(button.gameObject,new BaseEventData(events),ExecuteEvents.submitHandler);
        Require(click.isPlaying && click.ignoreListenerPause,"Keyboard submit or paused audio failed");
        Require(!hover.ignoreListenerVolume && !click.ignoreListenerVolume,"Audio ignores master volume");
        Require(UnityEngine.Object.FindObjectsByType<UIAudio>(FindObjectsSortMode.None).Length==1,"Duplicate shared players");
        AudioListener.pause=false;Time.timeScale=1;
        return "PASS: hover/disabled/right-click silence, click survives closing UI, keyboard submit, paused playback, master-volume routing and one shared player.";
    }
}
