using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Game.Casino;
using Player;
using Player.Equipment;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CasinoGateChecks
{
    private const string Folder = "Assets/Temporary/CasinoGateChecks";
    private const string ScenePath = Folder + "/Checks.unity";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string Prepare()
    {
        Require(!Application.isPlaying && !AssetDatabase.IsValidFolder(Folder), "Fixture already exists or Play Mode active");
        SessionState.SetString("CasinoGate.PreviousPlayScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        AssetDatabase.CreateFolder("Assets/Temporary", "CasinoGateChecks");
        var pedestal = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "CasinoPedistal");
        var door = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "CasinoDoor");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var gate = UnityEngine.Object.Instantiate(pedestal);
            var doors = UnityEngine.Object.Instantiate(door);
            SceneManager.MoveGameObjectToScene(gate, scene);
            SceneManager.MoveGameObjectToScene(doors, scene);
            doors.transform.SetParent(gate.transform, true);
            var data = new SerializedObject(gate.GetComponent<CasinoCagePedestal>());
            data.FindProperty("presentation").objectReferenceValue = doors.GetComponent<CasinoDoorPresentation>();
            data.ApplyModifiedPropertiesWithoutUndo();
            data = new SerializedObject(doors.GetComponent<CasinoDoorPresentation>());
            var animator = doors.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(Folder + "/Fixture.controller");
            var closedClip = new AnimationClip();
            var openClip = new AnimationClip();
            foreach (string part in new[] { "LeftDoor", "LeftCenter", "RightDoor", "RightCenter" })
            {
                float offset = part.StartsWith("Left") ? 1.35f : -1.35f;
                closedClip.SetCurve(part, typeof(Transform), "localPosition.z", AnimationCurve.Linear(0, 0, 1, 0));
                openClip.SetCurve(part, typeof(Transform), "localPosition.z", AnimationCurve.Linear(0, 0, 1, offset));
            }
            AssetDatabase.CreateAsset(closedClip, Folder + "/Closed.anim");
            AssetDatabase.CreateAsset(openClip, Folder + "/Opening.anim");
            controller.AddMotion(closedClip).name = "Closed";
            controller.AddMotion(openClip).name = "Opening";
            animator.runtimeAnimatorController = controller;
            data.FindProperty("doorAnimator").objectReferenceValue = animator;
            data.FindProperty("openingDuration").floatValue = 1f;
            var effect = new GameObject("FixtureEffects");
            effect.transform.SetParent(doors.transform, false);
            var light = effect.AddComponent<Light>();
            light.intensity = 2.3f;
            var particles = effect.AddComponent<ParticleSystem>();
            var main = particles.main; main.playOnAwake = false; main.loop = true;
            data.FindProperty("activationLights").arraySize = 1;
            data.FindProperty("activationLights").GetArrayElementAtIndex(0).objectReferenceValue = light;
            data.FindProperty("activationParticles").arraySize = 1;
            data.FindProperty("activationParticles").GetArrayElementAtIndex(0).objectReferenceValue = particles;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(gate, Folder + "/Gate.prefab");
            UnityEngine.Object.DestroyImmediate(gate);
            var player = new GameObject("GateCheckPlayer");
            SceneManager.MoveGameObjectToScene(player, scene);
            player.AddComponent<NetworkObject>(); player.AddComponent<Inventory>();
            PrefabUtility.SaveAsPrefabAsset(player, Folder + "/Player.prefab");
            UnityEngine.Object.DestroyImmediate(player);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        return "Isolated Casino fixture prepared; no save/game manager involved.";
    }

    public static string Restore()
    {
        Require(!Application.isPlaying, "Stop Play Mode");
        string previous = SessionState.GetString("CasinoGate.PreviousPlayScene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        AssetDatabase.DeleteAsset(Folder);
        SessionState.EraseString("CasinoGate.PreviousPlayScene");
        return "Fixture removed and Play Mode start scene restored.";
    }

    public static async Task<string> Run()
    {
        Require(Application.isPlaying && SceneManager.GetActiveScene().path == ScenePath, "Enter fixture Play Mode");
        NetworkManager host = null, client = null, late = null;
        GameObject registry = null;
        try
        {
            registry = new GameObject("FixtureItemRegistry"); registry.AddComponent<NetworkItemsHandler>();
            var gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Gate.prefab");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Player.prefab");
            var cagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Casino/CasinoCage.prefab");
            var wrongPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/EnemyCage.prefab");
            var prefabs = new[] { gatePrefab, playerPrefab, cagePrefab, wrongPrefab };
            host = Manager("GateHost", prefabs); host.SetSingleton(); Require(host.StartHost(), "Host start");
            client = Manager("GateClient", prefabs); Require(client.StartClient(), "Client start");
            await Until(() => client.IsConnectedClient, "Client connection");
            var gateObj = Spawn(gatePrefab, host, 0, gatePrefab.transform.position);
            var gate = gateObj.GetComponent<CasinoCagePedestal>();
            var player = Spawn(playerPrefab, host, client.LocalClientId, gate.transform.position + Vector3.left);
            var inventory = player.GetComponent<Inventory>();
            await Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(gateObj.NetworkObjectId) &&
                client.SpawnManager.SpawnedObjects.ContainsKey(player.NetworkObjectId), "Initial client objects");
            var clientGate = client.SpawnManager.SpawnedObjects[gateObj.NetworkObjectId].GetComponent<CasinoCagePedestal>();
            var ownerInventory = client.SpawnManager.SpawnedObjects[player.NetworkObjectId].GetComponent<Inventory>();
            Require(!gate.IsActivated && !Display(gate).activeSelf, "Gate initially empty/closed");
            Require(!Get<Light[]>(Presentation(gate), "activationLights")[0].enabled, "Light initially off");
            CheckPassage(gate, true);
            Request(clientGate, ownerInventory.NetworkObject);
            await Task.Delay(200);
            Require(!gate.IsActivated, "Empty hand rejected");

            var wrong = Spawn(wrongPrefab, host, 0, player.transform.position + Vector3.up);
            await Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(wrong.NetworkObjectId), "Wrong cage spawn");
            ownerInventory.AddItem(client.SpawnManager.SpawnedObjects[wrong.NetworkObjectId].gameObject);
            await Until(() => inventory.GetActiveItem() != null, "Wrong cage picked up");
            Request(clientGate, ownerInventory.NetworkObject);
            await Task.Delay(200);
            Require(!gate.IsActivated && wrong.IsSpawned, "Other cage rejected and preserved");
            inventory.ForgetItemServer(wrong.GetComponent<Item>()); wrong.Despawn(true);
            await Until(() => ownerInventory.GetActiveItem() == null, "Wrong item removed");

            var cage = Spawn(cagePrefab, host, 0, player.transform.position + Vector3.up);
            ulong cageId = cage.NetworkObjectId;
            await Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(cageId), "Casino cage spawn");
            ownerInventory.AddItem(client.SpawnManager.SpawnedObjects[cageId].gameObject);
            await Until(() => inventory.GetActiveItem() != null && ownerInventory.GetActiveItem() != null, "Casino cage picked up");
            player.transform.position += Vector3.left * 20;
            Request(clientGate, ownerInventory.NetworkObject);
            await Task.Delay(200);
            Require(!gate.IsActivated && cage.IsSpawned, "Out-of-range request rejected");
            player.transform.position = gate.transform.position + Vector3.left;

            var other = Spawn(playerPrefab, host, 0, player.transform.position);
            await Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(other.NetworkObjectId), "Other player spawn");
            Request(clientGate, client.SpawnManager.SpawnedObjects[other.NetworkObjectId]);
            await Task.Delay(200);
            Require(!gate.IsActivated, "Another player's inventory cannot be supplied");

            Request(clientGate, ownerInventory.NetworkObject);
            Request(clientGate, ownerInventory.NetworkObject);
            await Until(() => gate.IsActivated && clientGate.IsActivated, "Valid placement replicated");
            await Until(() => inventory.GetActiveItem() == null && ownerInventory.GetActiveItem() == null &&
                !host.SpawnManager.SpawnedObjects.ContainsKey(cageId) && !client.SpawnManager.SpawnedObjects.ContainsKey(cageId), "Consumed cage removed on all peers");
            Require(Display(gate).activeSelf && Display(clientGate).activeSelf, "Mounted cage visible on peers");
            Require(Get<Light[]>(Presentation(clientGate), "activationLights")[0].enabled &&
                Get<Light[]>(Presentation(clientGate), "activationLights")[0].intensity == 2.3f &&
                Get<ParticleSystem[]>(Presentation(clientGate), "activationParticles")[0].isPlaying, "Authored effects enabled without changing intensity");
            Require(Display(gate).GetComponentInChildren<Item>(true) == null, "Installed cage has no pickup behaviour");
            await Until(() => Presentation(gate).IsComplete && Presentation(clientGate).IsComplete, "Opening completed");
            await Task.Delay(150);
            CheckPassage(gate, false);
            Require(Vector3.Distance(Leaf(gate), Leaf(clientGate)) < 0.001f, "Host/client final door pose matches");

            late = Manager("GateLateClient", prefabs); Require(late.StartClient(), "Late join start");
            await Until(() => late.IsConnectedClient && late.SpawnManager.SpawnedObjects.ContainsKey(gateObj.NetworkObjectId), "Late join snapshot");
            var lateGate = late.SpawnManager.SpawnedObjects[gateObj.NetworkObjectId].GetComponent<CasinoCagePedestal>();
            await Until(() => lateGate.IsActivated && Presentation(lateGate).IsComplete, "Late join opens immediately");
            Require(Display(lateGate).activeSelf && Vector3.Distance(Leaf(gate), Leaf(lateGate)) < 0.001f, "Late join mounted cage and pose");
            var lateSound = Get<AudioSource>(Presentation(lateGate), "openingSound");
            Require(lateSound == null || !lateSound.isPlaying, "Late join does not replay sound");
            Require(Get<ParticleSystem[]>(Presentation(lateGate), "activationParticles")[0].isPlaying, "Late join looping effect enabled");
            return "PASS: empty/wrong item, distance, sender ownership, remote placement, duplicate requests, inventory/despawn, permanent mounted mesh, blocked/clear passage, host/client animation, late-join pose/display/silent audio.";
        }
        finally
        {
            if (late != null) late.Shutdown(); if (client != null) client.Shutdown(); if (host != null) host.Shutdown();
            await Task.Delay(400);
            if (late != null) UnityEngine.Object.Destroy(late.gameObject);
            if (client != null) UnityEngine.Object.Destroy(client.gameObject);
            if (host != null) UnityEngine.Object.Destroy(host.gameObject);
            if (registry != null) UnityEngine.Object.Destroy(registry);
        }
    }

    private static void CheckPassage(CasinoCagePedestal gate, bool blocked)
    {
        var door = Presentation(gate);
        Physics.SyncTransforms();
        var ray = new Ray(door.transform.position + Vector3.left * 3, Vector3.right);
        bool hit = door.GetComponentsInChildren<Collider>().Any(c => c.Raycast(ray, out _, 6));
        Require(hit == blocked, blocked ? "Closed door must block passage" : "Open door must clear passage");
    }
    private static NetworkObject Spawn(GameObject prefab, NetworkManager manager, ulong owner, Vector3 position)
    {
        var obj = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<NetworkObject>();
        typeof(NetworkObject).GetField("NetworkManagerOwner", Private | BindingFlags.Public).SetValue(obj, manager);
        obj.SpawnWithOwnership(owner); return obj;
    }
    private static NetworkManager Manager(string name, GameObject[] prefabs)
    {
        var go = new GameObject(name);
        var transport = go.AddComponent<UnityTransport>(); transport.SetConnectionData("127.0.0.1", 17859);
        var manager = go.AddComponent<NetworkManager>();
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, TickRate = 30 };
        foreach (var prefab in prefabs) manager.AddNetworkPrefab(prefab);
        return manager;
    }
    private static void Request(CasinoCagePedestal gate, NetworkObject player) =>
        typeof(CasinoCagePedestal).GetMethod("PlaceCageRpc", Private).Invoke(gate, new object[] { new NetworkObjectReference(player), default(RpcParams) });
    private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
    private static CasinoDoorPresentation Presentation(CasinoCagePedestal gate) => Get<CasinoDoorPresentation>(gate, "presentation");
    private static GameObject Display(CasinoCagePedestal gate) => Get<GameObject>(gate, "installedCage");
    private static Vector3 Leaf(CasinoCagePedestal gate) => Presentation(gate).transform.Find("LeftDoor").localPosition;
    private static async Task Until(Func<bool> predicate, string step)
    {
        double deadline = EditorApplication.timeSinceStartup + 12;
        while (!predicate()) { if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(step); await Task.Delay(25); }
    }
    private static void Require(bool condition, string step) { if (!condition) throw new InvalidOperationException(step); }
}
