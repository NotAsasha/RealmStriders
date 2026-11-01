using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FileSystem.Scripts;
using Player.Movement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using TMPro;
using Assets.Player.UI;

public static class CrouchChecks
{
    private const string Folder = "Assets/Temporary/CrouchChecks";
    private const string ScenePath = Folder + "/Checks.unity";
    private const string PrefabPath = Folder + "/Player.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string Prepare()
    {
        Require(!Application.isPlaying, "Stop Play Mode first");
        Require(!AssetDatabase.IsValidFolder(Folder), "Check assets already exist");
        SessionState.SetString("Crouch.PreviousPlayScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        AssetDatabase.CreateFolder("Assets/Temporary", "CrouchChecks");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var root = new GameObject("CrouchCheckPlayer");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<NetworkObject>();
            root.AddComponent<CharacterController>();
            var movement = root.AddComponent<PlayerMovement>();
            movement.audioSource = root.AddComponent<AudioSource>();
            movement.stepSounds = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab")
                .GetComponent<PlayerMovement>().stepSounds;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        return "Isolated movement/network fixture prepared; existing scenes untouched.";
    }

    public static string Restore()
    {
        Require(!Application.isPlaying, "Stop Play Mode first");
        string previous = SessionState.GetString("Crouch.PreviousPlayScene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        AssetDatabase.DeleteAsset(Folder);
        SessionState.EraseString("Crouch.PreviousPlayScene");
        return "Previous Play Mode start scene restored; fixture assets removed.";
    }

    public static async Task<string> Run()
    {
        Require(Application.isPlaying && SceneManager.GetActiveScene().path == ScenePath, "Enter fixture Play Mode");
        NetworkManager host = null, client = null, late = null;
        GameObject files = null, floor = null, ceiling = null;
        SettingsFile settings = null;
        Keyboard keyboard = null;
        var previousFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
        var previousBackground = InputSystem.settings.backgroundBehavior;
        try
        {
            files = new GameObject("CrouchCheckSettings");
            var handler = files.AddComponent<GameFileHandler>();
            settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SettingsFile>("Assets/FileSystem/SettingsFile.asset"));
            settings.save = new SettingsSave();
            handler.availableFiles = new System.Collections.Generic.List<GameFile> { settings };
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.EnableDevice(keyboard);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.transform.localScale = new Vector3(200, 1, 200);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            host = Manager("CrouchHost", prefab); host.SetSingleton();
            Require(host.StartHost(), "Host start");
            client = Manager("CrouchClient", prefab);
            Require(client.StartClient(), "Client start");
            await Until(() => client.IsConnectedClient, "Client join");
            var root = UnityEngine.Object.Instantiate(prefab, Vector3.up * 1.02f, Quaternion.identity);
            var obj = root.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetField("NetworkManagerOwner", Private | BindingFlags.Public).SetValue(obj, host);
            obj.SpawnWithOwnership(client.LocalClientId);
            root.GetComponent<CharacterController>().enabled = false;
            await Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(obj.NetworkObjectId), "Owner spawn");
            var owner = client.SpawnManager.SpawnedObjects[obj.NetworkObjectId].GetComponent<PlayerMovement>();
            var remote = root.GetComponent<PlayerMovement>();
            var capsule = owner.GetComponent<CharacterController>();
            await Task.Delay(300);
            float foot = capsule.center.y - capsule.height * 0.5f;
            await Keys(keyboard, Key.LeftCtrl);
            Require(owner.IsCrouching, "Hold crouch: owner=" + owner.IsOwner + " enabled=" + owner.enabled +
                " controller=" + capsule.enabled + " input=" + owner.controls.Gameplay.Crouch.IsPressed() +
                " key=" + keyboard.leftCtrlKey.isPressed + " spawned=" + owner.IsSpawned);
            Require(Mathf.Abs(capsule.height - 1.6f) < .001f, "Crouch capsule height");
            Require(Mathf.Abs(capsule.center.y - capsule.height * .5f - foot) < .001f, "Feet shifted");
            owner.isPaused = true; owner.SwitchToInteractionControls();
            await Task.Delay(100);
            owner.isPaused = false; owner.SwitchToGameplayControls();
            await Task.Delay(150);
            Require(owner.IsCrouching, "Held Ctrl lost after pause");
            await Until(() => remote.IsCrouching, "Client stance replicated to server");
            late = Manager("CrouchLateClient", prefab);
            Require(late.StartClient(), "Late join start");
            await Until(() => late.IsConnectedClient && late.SpawnManager.SpawnedObjects.ContainsKey(obj.NetworkObjectId), "Late join snapshot");
            var observer = late.SpawnManager.SpawnedObjects[obj.NetworkObjectId].GetComponent<PlayerMovement>();
            Require(observer.IsCrouching && Mathf.Abs(observer.GetComponent<CharacterController>().height - 1.6f) < .001f,
                "Late join stance/collider");
            observer.GetComponent<CharacterController>().enabled = false;
            await Keys(keyboard, Key.LeftCtrl, Key.W);
            await Task.Delay(450);
            Require(Mathf.Abs(Get<float>(owner, "currentSpeed") - 2.5f) < .01f, "Crouch speed");
            await Until(() => Mathf.Abs(owner.currentNoiseRadius.Value - owner.walkNoiseRadius * .35f) < .01f, "Quiet crouch noise");
            await Keys(keyboard, Key.LeftCtrl, Key.W, Key.LeftShift);
            await Task.Delay(300);
            Require(Mathf.Abs(Get<float>(owner, "currentSpeed") - 3.25f) < .01f, "Fast crouch speed");
            await Until(() => Mathf.Abs(owner.currentNoiseRadius.Value - owner.walkNoiseRadius) < .01f, "Fast crouch walking noise");
            await Keys(keyboard, Key.LeftCtrl);
            await Task.Delay(300);
            float y = owner.transform.position.y;
            await Keys(keyboard, Key.LeftCtrl, Key.Space);
            Require(owner.transform.position.y < y + .1f, "Jump while crouched");
            ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.position = new Vector3(owner.transform.position.x, owner.transform.position.y + .8f, owner.transform.position.z);
            ceiling.transform.localScale = new Vector3(4, .3f, 4);
            Physics.SyncTransforms();
            await Keys(keyboard);
            Require(owner.IsCrouching, "Stood through ceiling");
            ceiling.GetComponent<Collider>().isTrigger = true;
            await Task.Delay(200);
            Require(!owner.IsCrouching, "Trigger incorrectly blocked standing");
            ceiling.SetActive(false);
            await Until(() => !remote.IsCrouching && !observer.IsCrouching, "Standing replicated");
            settings.save.crouchMode = CrouchMode.Toggle;
            settings.GetData();
            await Keys(keyboard, Key.LeftCtrl);
            owner.isPaused = true; owner.SwitchToInteractionControls();
            await Task.Delay(100);
            owner.isPaused = false; owner.SwitchToGameplayControls();
            await Task.Delay(150);
            Require(owner.IsCrouching, "Held Ctrl toggled again after pause");
            await Keys(keyboard);
            Require(owner.IsCrouching, "Toggle did not latch");
            owner.isPaused = true; owner.SwitchToInteractionControls();
            await Keys(keyboard, Key.LeftCtrl); await Keys(keyboard);
            Require(owner.IsCrouching, "Pause changed crouch");
            owner.isPaused = false; owner.SwitchToGameplayControls();
            await Keys(keyboard, Key.LeftCtrl); await Keys(keyboard);
            Require(!owner.IsCrouching, "Second toggle did not stand");
            await Keys(keyboard, Key.LeftCtrl); await Keys(keyboard);
            owner.ResetMovementState();
            Require(!owner.IsCrouching, "Teleport reset did not clear stance");
            await Keys(keyboard, Key.LeftCtrl); await Keys(keyboard);
            Invoke(owner, "OnDeathChanged", false, true);
            Require(!owner.IsCrouching && owner.currentNoiseRadius.Value == 0, "Death reset");
            obj.Despawn();
            await Until(() => PlayerMovement.Instance == null, "Despawn cleanup");
            var oldSave = new SettingsSave();
            JsonUtility.FromJsonOverwrite("{\"sensValue\":0.7}", oldSave);
            Require(oldSave.crouchMode == CrouchMode.Hold, "Old settings compatibility");
            var roundTrip = JsonUtility.FromJson<SettingsSave>(JsonUtility.ToJson(settings.save));
            Require(roundTrip.crouchMode == CrouchMode.Toggle, "Mode serialization");
            return "PASS: client-owned movement, host + late client stance/capsule; hold/toggle; speeds/noise; jump blocked; ceiling blocks standing; triggers ignored; pause; teleport/death/despawn resets; old/new settings serialization.";
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousFocus;
            InputSystem.settings.backgroundBehavior = previousBackground;
            if (late != null) late.Shutdown(); if (client != null) client.Shutdown(); if (host != null) host.Shutdown();
            await Task.Delay(150);
            foreach (var manager in new[] { late, client, host }) if (manager != null) UnityEngine.Object.Destroy(manager.gameObject);
            foreach (var go in new[] { files, floor, ceiling }) if (go != null) UnityEngine.Object.Destroy(go);
            if (settings != null) UnityEngine.Object.Destroy(settings);
        }
    }

    public static string Preview()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(1280, 720, 24);
        var previous = RenderTexture.active;
        Camera camera = null;
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/UI/Settings.prefab"), scene);
            var cam = new GameObject("CrouchPreview", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cam, scene);
            camera = cam.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.targetTexture = target; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            root.SetActive(true);
            root.GetComponent<SettingsMenu>().SelectCategory(2);
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes("tools/crouch-settings.png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            return "Controls preview: tools/crouch-settings.png";
        }
        finally
        {
            if (camera != null) camera.targetTexture = null;
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static NetworkManager Manager(string name, GameObject prefab)
    {
        var go = new GameObject(name);
        var transport = go.AddComponent<UnityTransport>(); transport.SetConnectionData("127.0.0.1", 17849);
        var manager = go.AddComponent<NetworkManager>();
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, TickRate = 30 };
        manager.AddNetworkPrefab(prefab);
        return manager;
    }
    private static async Task Keys(Keyboard keyboard, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        await Task.Delay(180);
    }
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static async Task Until(Func<bool> condition, string step)
    {
        double deadline = EditorApplication.timeSinceStartup + 8;
        while (!condition()) { if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(step); await Task.Delay(20); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
