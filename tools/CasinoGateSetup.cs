using System;
using System.Linq;
using Game.Casino;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CasinoGateSetup
{
    public static string Configure()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/Scenes/Casino/Casino.unity")
            throw new InvalidOperationException("Open Casino in Edit Mode.");
        // Retire only the effects created by the previous procedural setup.
        Disable(Root("CasinoDoor").transform.Find("GateLight"));
        Disable(Root("CasinoPedistal").transform.Find("ChipPoint/ActivationLight"));
        Disable(Root("CasinoPedistal").transform.Find("ChipPoint/ActivationRing"));
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return Validate();
    }

    public static string Validate()
    {
        var gate = Root("CasinoPedistal").GetComponent<CasinoCagePedestal>();
        if (gate == null || gate.gameObject.layer != 6) throw new Exception("Missing interactable pedestal");
        var data = new SerializedObject(gate);
        foreach (var field in new[] { "acceptedCagePrefab", "placementPoint", "installedCage", "presentation" })
            if (data.FindProperty(field).objectReferenceValue == null) throw new Exception("Missing " + field);
        var display = (GameObject)data.FindProperty("installedCage").objectReferenceValue;
        if (display.GetComponentsInChildren<Component>(true).Any(c => !(c is Transform || c is MeshFilter || c is MeshRenderer)))
            throw new Exception("Installed cage must contain only meshes");
        if (display.activeSelf) throw new Exception("Cage must be hidden before placement");
        return "PASS: pedestal wiring preserved. Assign authored Animator, lights and particles on CasinoDoorPresentation.";
    }

    private static void Disable(Transform target)
    {
        if (target == null) return;
        Undo.RecordObject(target.gameObject, "Disable old Casino gate effect");
        target.gameObject.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target.gameObject);
    }

    private static GameObject Root(string name) => SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == name);
}
