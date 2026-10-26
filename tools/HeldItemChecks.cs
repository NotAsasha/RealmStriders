using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Player;
using Player.Equipment;
using UnityEditor;
using UnityEngine;

// Execute entry points with Unity MCP run_script. Copies used by checks are always destroyed.
public static class HeldItemChecks
{
    public static string SaveConfiguredPrefabs(string[] paths)
    {
        foreach (string path in paths)
            PrefabUtility.SavePrefabAsset(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        return $"Saved {paths.Length} configured prefabs.";
    }

    public static string ConfigureGrip(string path, float x, float y, float z)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Item item = root.GetComponent<Item>();
            Transform grip = item.gripPoint;
            if (grip == null)
            {
                grip = new GameObject("GripPoint").transform;
                grip.SetParent(root.transform, false);
                item.gripPoint = grip;
            }
            grip.localPosition = new Vector3(x, y, z);
            grip.localRotation = Quaternion.identity;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return "Configured grip: " + path;
    }

    public static string ValidatePrefabs(string[] paths)
    {
        string[] fields = { "inventoryRenderers", "inventoryLights", "inventoryCanvases", "inventoryAudioSources", "inventoryColliders" };
        Type[] types = { typeof(Renderer), typeof(Light), typeof(Canvas), typeof(AudioSource), typeof(Collider) };
        foreach (string path in paths)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var serialized = new SerializedObject(root.GetComponent<Item>());
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty array = serialized.FindProperty(fields[i]);
                Component[] expected = root.GetComponentsInChildren(types[i], true);
                Check(array.arraySize == expected.Length, path + ": incomplete " + fields[i]);
                for (int j = 0; j < array.arraySize; j++)
                    Check(array.GetArrayElementAtIndex(j).objectReferenceValue != null, path + ": null " + fields[i]);
            }
        }
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab");
        var holder = new SerializedObject(player.GetComponent<Inventory>().ItemHolder);
        foreach (string field in new[] { "inventory", "movement", "cameraMovement", "cameraTransform", "handTarget", "heldItemSocket" })
            Check(holder.FindProperty(field).objectReferenceValue != null, "Player: missing " + field);
        return $"PASS: presentation references on {paths.Length} items and all holder references.";
    }

    public static string CheckGripAndPresentation()
    {
        GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Player/Equipment/WalkieTalkie/WalkieTalkie.prefab"));
        try
        {
            Item item = root.GetComponent<Item>();
            typeof(Item).GetMethod("CacheComponents", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(item, null);
            var positions = new[] { Vector3.zero, new Vector3(3f, -2f, 4f) };
            foreach (float scale in new[] { 0.5f, 1f, 1.2f })
            foreach (Vector3 position in positions)
            {
                root.transform.localScale = Vector3.one * scale;
                Quaternion rotation = Quaternion.Euler(25f, 123f, -38f);
                item.AlignToGripPose(position, rotation);
                Check(Vector3.Distance(item.gripPoint.position, position) < 0.0001f, "Grip position drift");
                Check(Quaternion.Angle(item.gripPoint.rotation, rotation) < 0.01f, "Grip rotation drift");
            }
            item.SetInventoryVisible(false);
            Check(root.activeSelf, "Stored network root was disabled");
            Check(root.GetComponentsInChildren<Renderer>(true).All(r => r.forceRenderingOff), "Stored mesh visible");
            item.SetInventoryVisible(true);
            Check(root.GetComponentsInChildren<Renderer>(true).All(r => !r.forceRenderingOff), "Equipped mesh hidden");
            return "PASS: grip alignment at multiple scales/poses; stored root stays active; presentation restores.";
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static object PreviewRig(string itemPath, string imagePath, bool firstPerson = true)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject player = null;
        RenderTexture texture = null;
        Texture2D pixels = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab"));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(player, scene);
            foreach (Canvas canvas in player.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            Inventory inventory = player.GetComponent<Inventory>();
            typeof(Inventory).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inventory, null);
            PlayerHeadSync head = player.GetComponentInChildren<PlayerHeadSync>(true);
            typeof(PlayerHeadSync).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(head, null);
            var rig = player.GetComponentInChildren<UnityEngine.Animations.Rigging.Rig>(true);
            rig.weight = 1f;
            GameObject itemRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(itemPath));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(itemRoot, scene);
            itemRoot.transform.SetParent(player.transform, true);
            foreach (Light itemLight in itemRoot.GetComponentsInChildren<Light>(true)) itemLight.gameObject.SetActive(false);
            foreach (ParticleSystem particle in itemRoot.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);
            Item item = itemRoot.GetComponent<Item>();
            var items = (Item[])typeof(Inventory).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory);
            items[0] = item;
            inventory.ItemHolder.SetActiveItem(item);
            if (firstPerson) inventory.ItemHolder.EvaluateViewModelAndIKTarget(head.ShoulderBone, head.MinSafeArmReach, head.MaxSafeArmReach);
            else inventory.ItemHolder.EvaluateRemotePose(head.ShoulderBone, head.MinSafeArmReach, head.MaxSafeArmReach, 0f);
            typeof(PlayerHeadSync).GetMethod("ApplyRightHandIK", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(head, null);
            inventory.ItemHolder.AlignItemToHand(item);
            var holderFields = new SerializedObject(inventory.ItemHolder);
            Vector3 palm = inventory.HandAnchor.TransformPoint(holderFields.FindProperty("palmPositionLocal").vector3Value);
            if (item.gripPoint != null) Check(Vector3.Distance(palm, item.gripPoint.position) < 0.0001f, "Solved palm detached from grip");

            Camera camera = player.GetComponentInChildren<Camera>(true);
            camera.scene = scene;
            camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            camera.aspect = 16f / 9f;
            camera.useOcclusionCulling = false;
            Vector3 viewport = camera.WorldToViewportPoint(palm);
            if (!firstPerson)
            {
                camera.transform.SetParent(player.transform, true);
                camera.transform.position = palm + new Vector3(0.7f, 0.2f, 0.9f);
                camera.transform.LookAt(palm);
            }
            var light = new GameObject("Preview Light");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light, scene);
            light.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            light.AddComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 2f;
            texture = new RenderTexture(1280, 720, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            System.IO.File.WriteAllBytes(imagePath, pixels.EncodeToPNG());
            return new { imagePath, palmViewport = viewport.ToString("F3"), palm = palm.ToString("F3") };
        }
        finally
        {
            RenderTexture.active = previous;
            if (player != null)
            {
                Camera camera = player.GetComponentInChildren<Camera>(true);
                if (camera != null) camera.targetTexture = null;
            }
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
