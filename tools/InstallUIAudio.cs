using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.UI;

public static class InstallUIAudio
{
    private static readonly string[] Folders={"Assets/Base","Assets/Player","Assets/Scenes","Assets/Steam"};
    private static bool Supported(Selectable s)=>s is Button || s is Toggle || s is TMP_Dropdown || s is Dropdown;
    private static string[] Prefabs()=>AssetDatabase.FindAssets("t:Prefab",Folders).Select(AssetDatabase.GUIDToAssetPath)
        .OrderBy(p=>AssetDatabase.GetDependencies(p).Length).ToArray();
    public static string Audit()
    {
        var result=new List<string>();int count=0;
        foreach(var path in Prefabs())
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var controls=root.GetComponentsInChildren<Selectable>(true).Where(Supported).ToArray();
            if(controls.Length==0)continue;count+=controls.Length;result.Add(path+": "+controls.Length);
            foreach(var b in controls.OfType<Button>())for(int i=0;i<b.onClick.GetPersistentEventCount();i++)
                if(b.onClick.GetPersistentTarget(i) is AudioSource)result.Add("EXISTING AUDIO: "+path+" "+b.name);
        }
        return string.Join("\n",result)+"\nTotal prefab controls including inherited instances: "+count;
    }
    private static int Wire(GameObject root)
    {
        int changes=0;
        foreach(var control in root.GetComponentsInChildren<Selectable>(true).Where(Supported))
        {
            var sound=control.GetComponent<UIControlSound>();
            if(sound==null)
            {
                sound=control.gameObject.AddComponent<UIControlSound>();
                var data=new SerializedObject(sound);data.FindProperty("control").objectReferenceValue=control;data.ApplyModifiedPropertiesWithoutUndo();changes++;
            }
            if(!(control is Button button))continue;
            for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)
            {
                if(!(button.onClick.GetPersistentTarget(i) is AudioSource oldAudio) || button.onClick.GetPersistentMethodName(i)!="Play")continue;
                string clip=AssetDatabase.GetAssetPath(oldAudio.clip);
                if(clip=="Assets/Player/Equipment/BatteryClick.wav" || clip=="Assets/Player/Equipment/Click1.mp3" || clip=="Assets/Player/Equipment/Click2.mp3")
                {UnityEventTools.RemovePersistentListener(button.onClick,i);changes++;}
            }
            int callback=-1;
            for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
                if(button.onClick.GetPersistentTarget(i)==sound && button.onClick.GetPersistentMethodName(i)==nameof(UIControlSound.PlayClick))callback=i;
            if(callback<0)
            {
                UnityEventTools.AddPersistentListener(button.onClick,sound.PlayClick);
                callback=button.onClick.GetPersistentEventCount()-1;changes++;
            }
            if(callback>0)
            {
                var data=new SerializedObject(button);data.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").MoveArrayElement(callback,0);data.ApplyModifiedPropertiesWithoutUndo();changes++;
            }
        }
        return changes;
    }
    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save scene edits before installing UI audio");
        const string audioPath="Assets/Resources/UIAudio.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(audioPath)==null)
        {
            var root=new GameObject("UI Audio");
            try
            {
                var audio=root.AddComponent<UIAudio>();var hover=root.AddComponent<AudioSource>();var click=root.AddComponent<AudioSource>();
                foreach(var source in new[]{hover,click}){source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.ignoreListenerPause=true;source.volume=1;source.priority=32;}
                var data=new SerializedObject(audio);data.FindProperty("hoverSource").objectReferenceValue=hover;data.FindProperty("clickSource").objectReferenceValue=click;
                data.FindProperty("hoverClip").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Player/Equipment/Click1.mp3");
                data.FindProperty("clickClip").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Player/Equipment/Click2.mp3");data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,audioPath);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        var changed=new List<string>();
        foreach(var path in Prefabs())
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!asset.GetComponentsInChildren<Selectable>(true).Any(Supported))continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try{int n=Wire(root);if(n>0){PrefabUtility.SaveAsPrefabAsset(root,path);changed.Add(path+" ("+n+")");}}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var previous=SceneManager.GetActiveScene();
        foreach(var path in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                int n=0;foreach(var root in scene.GetRootGameObjects())n+=Wire(root);
                if(n>0){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);changed.Add(path+" ("+n+")");}
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        SceneManager.SetActiveScene(previous);
        return "UI audio installed; modified assets:\n"+string.Join("\n",changed);
    }
    private static int VerifyRoot(GameObject root)
    {
        int count=0;
        foreach(var chooser in root.GetComponentsInChildren<CategorySwitch>(true))
        {
            for(int index=0;index<chooser.buttonComponents.Length;index++)
            {
                var button=chooser.buttonComponents[index];bool found=false;
                var calls=new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                for(int i=0;i<calls.arraySize;i++)
                    if(button.onClick.GetPersistentTarget(i)==chooser && button.onClick.GetPersistentMethodName(i)=="SwitchToItem" &&
                        calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Arguments.m_IntArgument").intValue==index)found=true;
                if(!found)throw new Exception("Audio setup displaced Shop category callback: "+index);
            }
        }
        foreach(var control in root.GetComponentsInChildren<Selectable>(true).Where(Supported))
        {
            count++;var sound=control.GetComponent<UIControlSound>();
            if(sound==null || new SerializedObject(sound).FindProperty("control").objectReferenceValue!=control)throw new Exception("Missing audio reference: "+control.name);
            if(control is Button b)
            {
                if(b.onClick.GetPersistentEventCount()==0 || b.onClick.GetPersistentTarget(0)!=sound || b.onClick.GetPersistentMethodName(0)!=nameof(UIControlSound.PlayClick))throw new Exception("Audio must run before navigation: "+b.name);
                int callbacks=Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Count(i=>b.onClick.GetPersistentTarget(i) is UIControlSound);
                if(callbacks!=1)throw new Exception("Duplicate sound callback: "+b.name);
            }
        }
        return count;
    }
    public static string Verify()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first");
        int count=0;foreach(var path in Prefabs())count+=VerifyRoot(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        var previous=SceneManager.GetActiveScene();
        foreach(var path in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try{foreach(var root in scene.GetRootGameObjects())count+=VerifyRoot(root);}
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        SceneManager.SetActiveScene(previous);
        return "PASS: "+count+" controls across game prefabs and scenes, including inherited instances, have valid audio references and single button callbacks before navigation.";
    }
}
