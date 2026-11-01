using System;
using Enemy.Shocker;
using UnityEditor;
using UnityEngine;

public static class ShockerCageSetup
{
    public static string Configure()
    {
        const string path = "Assets/Enemy/Shocker/ShockerCage.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var effect = root.GetComponent<ShockerCageDischarge>();
            if (effect == null) effect = root.AddComponent<ShockerCageDischarge>();
            var child = root.transform.Find("AutomaticDischarge");
            if (child == null)
            {
                child = new GameObject("AutomaticDischarge").transform;
                child.SetParent(root.transform, false);
            }
            var sphere = root.transform.Find("TeslaCoil/Icosphere");
            if (sphere == null) throw new Exception("Missing authored coil sphere");
            child.position = sphere.GetComponent<Renderer>().bounds.center;
            var line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
            const string materialPath = "Assets/Enemy/Shocker/ShockerCageBolt.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", new Color(0.3f, 0.85f, 1f));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 9;
            line.startWidth = 0.045f;
            line.endWidth = 0.018f;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            var sound = child.GetComponent<AudioSource>();
            if (sound == null) sound = child.gameObject.AddComponent<AudioSource>();
            var taser = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Equipment/Taser/Taser.prefab");
            var taserData = new SerializedObject(taser.GetComponent<Player.Equipment.Taser.Taser>());
            var taserSound = (AudioSource)taserData.FindProperty("sound").objectReferenceValue;
            sound.clip = taserSound.clip;
            sound.outputAudioMixerGroup = taserSound.outputAudioMixerGroup;
            sound.playOnAwake = false;
            sound.spatialBlend = 1f;
            sound.minDistance = 1f;
            sound.maxDistance = 20f;
            sound.rolloffMode = AudioRolloffMode.Linear;
            var data = new SerializedObject(effect);
            data.FindProperty("dischargeOrigin").objectReferenceValue = child;
            data.FindProperty("bolt").objectReferenceValue = line;
            data.FindProperty("dischargeSound").objectReferenceValue = sound;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return "ShockerCage configured; audio=" + sound.clip.name;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static string Validate()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Shocker/ShockerCage.prefab");
        var effect = root.GetComponent<ShockerCageDischarge>();
        if (effect == null) throw new Exception("Missing discharge component");
        var data = new SerializedObject(effect);
        foreach (string field in new[] { "dischargeOrigin", "bolt", "dischargeSound" })
            if (data.FindProperty(field).objectReferenceValue == null) throw new Exception("Missing " + field);
        var bolt = (LineRenderer)data.FindProperty("bolt").objectReferenceValue;
        var audio = (AudioSource)data.FindProperty("dischargeSound").objectReferenceValue;
        if (bolt.enabled || bolt.positionCount != 9 || bolt.sharedMaterial == null || audio.clip == null || audio.playOnAwake)
            throw new Exception("Invalid presentation configuration");
        if (root.GetComponent<Player.Equipment.Item>() == null || root.GetComponent<Unity.Netcode.NetworkObject>() == null)
            throw new Exception("Missing item/network components");
        return "PASS: references, bolt material/vertices, audio, Item and NetworkObject";
    }
}
