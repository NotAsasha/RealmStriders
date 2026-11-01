using System;
using System.Linq;
using Game.Casino;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CasinoAnimatorRepair
{
    private const string PrefabPath = "Assets/Scenes/Casino/CasinoDoor.prefab";
    private const string ControllerPath = "Assets/Scenes/Casino/Door.controller";

    public static string Repair()
    {
        if (Application.isPlaying) throw new Exception("Stop Play Mode first");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Scenes/Casino/Opening.anim");
        if (controller == null || clip == null) throw new Exception("Missing authored controller/clip");
        var layers = controller.layers;
        if (layers.Length != 2 || layers[0].name != "Base Layer.Closed" || layers[1].name != "Base Layer.Opening")
            throw new Exception("Controller changed since inspection; inspect before repairing");
        Undo.RegisterCompleteObjectUndo(controller, "Fix Casino Animator states");
        var machine = layers[0].stateMachine;
        Undo.RegisterCompleteObjectUndo(machine, "Fix Casino Animator states");
        machine.name = "Base Layer";
        var closed = machine.states.Single().state;
        Undo.RecordObject(closed, "Configure closed pose");
        closed.name = "Closed";
        // Hold frame zero of the owner's clip; no replacement animation is generated.
        closed.motion = clip;
        closed.speed = 0f;
        machine.defaultState = closed;
        var opening = machine.AddState("Opening", new Vector3(400, 100, 0));
        opening.motion = clip;
        opening.speed = 1f;
        layers[0].name = "Base Layer";
        layers[0].defaultWeight = 1f;
        controller.layers = new[] { layers[0] };
        EditorUtility.SetDirty(controller);

        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Configure(prefab, clip.length);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var scene = SceneManager.GetActiveScene();
        var root = scene.GetRootGameObjects().Single(g => g.name == "CasinoDoor");
        Configure(root, clip.length);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Repaired controller: Base Layer with Closed (frame zero) and Opening; duration=" + clip.length;
    }

    private static void Configure(GameObject root, float duration)
    {
        var presentation = root.GetComponentInChildren<CasinoDoorPresentation>(true);
        var animator = root.GetComponent<Animator>();
        if (presentation == null || animator == null) throw new Exception("Missing authored components");
        Undo.RecordObject(presentation, "Fix Casino Animator references");
        var data = new SerializedObject(presentation);
        data.FindProperty("doorAnimator").objectReferenceValue = animator;
        data.FindProperty("closedState").stringValue = "Base Layer.Closed";
        data.FindProperty("openingState").stringValue = "Base Layer.Opening";
        data.FindProperty("animatorLayer").intValue = 0;
        data.FindProperty("openingDuration").floatValue = duration;
        data.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(presentation);
    }

    public static string Verify()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            SceneManager.MoveGameObjectToScene(root, scene);
            var animator = root.GetComponent<Animator>();
            var presentation = root.GetComponentInChildren<CasinoDoorPresentation>(true);
            var left = root.transform.Find("CasinoDoor/LeftDoor");
            var right = root.transform.Find("CasinoDoor/RightDoor");
            animator.Rebind();
            animator.Update(0f);
            Require(animator.layerCount == 1 && animator.HasState(0, Animator.StringToHash("Base Layer.Closed")) &&
                animator.HasState(0, Animator.StringToHash("Base Layer.Opening")), "State paths resolve; layers=" + animator.layerCount + ", initialized=" + animator.isInitialized);
            foreach (var binding in AnimationUtility.GetCurveBindings(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Scenes/Casino/Opening.anim")))
                Require(root.transform.Find(binding.path) != null, "Animation binding: " + binding.path);
            presentation.ResetPresentation();
            Vector3 leftClosed = left.localPosition, rightClosed = right.localPosition;
            animator.Update(0.5f);
            Require(Vector3.Distance(leftClosed, left.localPosition) < 0.001f, "Closed state holds pose");
            presentation.Sample(0f, false);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Opening"), "Opening state entered");
            animator.Update(presentation.Duration);
            Vector3 leftOpen = left.localPosition, rightOpen = right.localPosition;
            Require(Vector3.Distance(leftClosed, leftOpen) > 0.1f && Vector3.Distance(rightClosed, rightOpen) > 0.1f, "Authored clip moves both leaves");
            animator.Update(1f);
            Require(Vector3.Distance(leftOpen, left.localPosition) < 0.001f, "Final pose stays open");
            presentation.ResetPresentation();
            Require(Vector3.Distance(leftClosed, left.localPosition) < 0.001f, "Reset restores closed pose");
            presentation.Sample(presentation.Duration + 10f, false);
            Require(Vector3.Distance(leftOpen, left.localPosition) < 0.001f &&
                Vector3.Distance(rightOpen, right.localPosition) < 0.001f, "Late join seeks to final pose");
            return "PASS: actual authored controller states, all clip bindings, closed hold, both leaves opening, final hold, reset and late-join pose.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static string NormalizeMachineName()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        Undo.RecordObject(machine, "Match Animator layer/state machine name");
        machine.name = "Base Layer";
        EditorUtility.SetDirty(machine);
        AssetDatabase.SaveAssets();
        return "State machine name matches Base Layer";
    }
}
