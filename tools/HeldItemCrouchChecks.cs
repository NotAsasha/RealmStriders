using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Player;
using Player.Equipment;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering.Universal;

public static class HeldItemCrouchChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string[] ItemPaths() => AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Player/Equipment","Assets/Enemy","Assets/Base"})
        .Select(AssetDatabase.GUIDToAssetPath).Where(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<Item>()!=null).ToArray();

    public static string Configure()
    {
        var result = new StringBuilder();
        foreach (string path in ItemPaths())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var item = root.GetComponent<Item>();
                var effect = EffectRoot(item);
                bool found = false; Bounds bounds = default;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (effect != null && renderer.transform.IsChildOf(effect)) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    Bounds mesh = filter.sharedMesh.bounds;
                    for (int i=0;i<8;i++)
                    {
                        Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        Vector3 point = root.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                        if (!found) { bounds = new Bounds(point,Vector3.zero); found=true; }
                        else bounds.Encapsulate(point);
                    }
                }
                if (!found) throw new InvalidOperationException("Missing physical mesh: "+path);
                var so=new SerializedObject(item); so.FindProperty("heldCameraBounds").boundsValue=bounds; so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
                result.AppendLine(root.name+" bounds="+bounds);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return result.ToString();
    }

    private static Transform EffectRoot(Item item)
    {
        var property = new SerializedObject(item).FindProperty("effectBubble");
        return property != null && property.objectReferenceValue is GameObject effect ? effect.transform : null;
    }
    public static string Run(float forward, bool pictures)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(960, 540, 24);
        var previous = RenderTexture.active;
        Camera camera = null;
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab"), scene);
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            var inventory = root.GetComponent<Inventory>(); Invoke(inventory, "Awake");
            var head = root.GetComponentInChildren<PlayerHeadSync>(true); Invoke(head, "Awake");
            foreach (var rig in root.GetComponentsInChildren<RigBuilder>(true)) rig.enabled = false;
            var handRig = root.GetComponentInChildren<Rig>(true); handRig.weight = 1;
            var animator = root.GetComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false; animator.Rebind(); animator.Update(0);
            animator.SetFloat("StandingSpeed", 1); animator.SetFloat("CrouchMoveSpeed", 1);
            camera = root.GetComponentInChildren<Camera>(true);
            var cameraMovement = root.GetComponentInChildren<CameraMovement>(true);
            Invoke(cameraMovement, "CacheFirstPersonVisibility"); Invoke(cameraMovement, "SetFirstPersonVisibility", true);
            camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.25f,.35f,.45f);
            camera.targetTexture = target; camera.aspect = 16f / 9;
            var light = new GameObject("HeldCheckLight", typeof(Light)); SceneManager.MoveGameObjectToScene(light, scene);
            light.GetComponent<Light>().type = LightType.Directional; light.GetComponent<Light>().intensity = 2;
            light.transform.rotation = Quaternion.Euler(30,-140,0);
            var items = (Item[])typeof(Inventory).GetField("items", Private).GetValue(inventory);
            var result = new StringBuilder();
            var paths = ItemPaths();
            foreach (string path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<Item>() == null) continue;
                var itemRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                itemRoot.transform.SetParent(root.transform, true);
                foreach (var l in itemRoot.GetComponentsInChildren<Light>(true)) l.enabled = false;
                foreach (var p in itemRoot.GetComponentsInChildren<ParticleSystem>(true)) p.gameObject.SetActive(false);
                var item = itemRoot.GetComponent<Item>(); items[0] = item; inventory.ItemHolder.SetActiveItem(item);
                var effect = EffectRoot(item); if (effect != null) effect.gameObject.SetActive(false);
                float minimum = float.MaxValue; string worst = "";
                foreach (string state in new[]{"mixamo_com","Crouch Idle","Crouch Walk"})
                foreach (float phase in new[]{0f,.25f,.5f,.75f})
                foreach (float pitch in new[]{-80f,-45f,0f,45f,80f})
                foreach (float equipWeight in new[]{0f,.25f,.5f,1f})
                {
                    handRig.weight = equipWeight;
                    Invoke(head,"RestoreFirstPersonShoulder");
                    animator.SetBool("Crouched", state != "mixamo_com"); animator.SetBool("Moving",state=="Crouch Walk");
                    animator.Play(state,0,phase); animator.Update(0);
                    float bodyPitch = Mathf.Clamp(pitch, head.MinPitchAngle, head.MaxPitchAngle);
                    head.spineBone.localRotation *= Quaternion.Euler(bodyPitch * head.spinePitchFraction,0,0);
                    head.headBone.localRotation *= Quaternion.Euler(bodyPitch * (1-head.spinePitchFraction),0,0);
                    cameraMovement.transform.localPosition = state == "mixamo_com" ? new Vector3(0,.7f,0) : new Vector3(0,.05f,forward);
                    cameraMovement.transform.localRotation = Quaternion.Euler(pitch,0,0);
                    typeof(CameraMovement).GetField("xRotation",Private).SetValue(cameraMovement,pitch);
                    inventory.ItemHolder.EvaluateViewModelAndIKTarget(head.ShoulderBone,head.MinSafeArmReach,head.MaxSafeArmReach);
                    Vector3 shoulderBefore = head.ShoulderBone.localPosition;
                    var constraint = root.GetComponentInChildren<TwoBoneIKConstraint>(true);
                    float upperLength = Vector3.Distance(constraint.data.root.position,constraint.data.mid.position);
                    float lowerLength = Vector3.Distance(constraint.data.mid.position,constraint.data.tip.position);
                    Invoke(head,"ApplyRightHandIK"); inventory.ItemHolder.AlignItemToHand(item);
                    float depth = MinimumDepth(itemRoot,camera);
                    if (depth < .119f) throw new InvalidOperationException("Camera clipping: "+path+" depth="+depth+" equip="+equipWeight);
                    if (Mathf.Abs(upperLength-Vector3.Distance(constraint.data.root.position,constraint.data.mid.position))>.0001f ||
                        Mathf.Abs(lowerLength-Vector3.Distance(constraint.data.mid.position,constraint.data.tip.position))>.0001f)
                        throw new InvalidOperationException("Arm segment stretched: "+path);
                    if (depth < minimum) { minimum=depth; worst=state+" pitch="+pitch+" phase="+phase; }
                    if (pictures && equipWeight==1 && phase==.5f && pitch==0 && (item.name.StartsWith("Lantern") || item.name.StartsWith("LeafBlower")))
                        Capture(camera,target,"tools/held-"+item.name+"-"+state.Replace(" ","-")+".png");
                    if (item.gripPoint != null)
                    {
                        var palm = new SerializedObject(inventory.ItemHolder).FindProperty("palmPositionLocal").vector3Value;
                        if (Vector3.Distance(item.gripPoint.position, inventory.HandAnchor.TransformPoint(palm))>.0001f)
                            throw new InvalidOperationException("Detached grip: "+path);
                    }
                    Invoke(head,"RestoreFirstPersonShoulder");
                    if (Vector3.Distance(shoulderBefore,head.ShoulderBone.localPosition)>.0001f)
                        throw new InvalidOperationException("Shoulder drift: "+path);
                    inventory.ItemHolder.EvaluateRemotePose(head.ShoulderBone,head.MinSafeArmReach,head.MaxSafeArmReach,pitch);
                    Invoke(head,"ApplyRightHandIK");
                    if (inventory.ItemHolder.RequiresFirstPersonReach || Vector3.Distance(shoulderBefore,head.ShoulderBone.localPosition)>.0001f)
                        throw new InvalidOperationException("Local correction leaked into remote pose: "+path);
                }
                result.AppendLine(prefab.name+" minDepth="+minimum.ToString("F3")+" at "+worst);
                Invoke(head,"RestoreFirstPersonShoulder");
                items[0]=null; inventory.ItemHolder.SetActiveItem(null); UnityEngine.Object.DestroyImmediate(itemRoot);
            }
            return result.ToString();
        }
        finally
        {
            if (camera != null) camera.targetTexture=null;
            RenderTexture.active=previous; EditorSceneManager.ClosePreviewScene(scene);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
    private static float MinimumDepth(GameObject item, Camera camera)
    {
        float minimum=float.MaxValue;
        foreach (var renderer in item.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.gameObject.activeInHierarchy) continue;
            Bounds b=renderer.localBounds;
            for(int i=0;i<8;i++)
            {
                var corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                minimum=Mathf.Min(minimum,camera.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner)).z);
            }
        }
        return minimum;
    }
    private static void Invoke(object target,string method,params object[] args) => target.GetType().GetMethod(method,Private).Invoke(target,args);
    private static void Capture(Camera camera,RenderTexture target,string path)
    {
        camera.Render(); RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
        File.WriteAllBytes(path,image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
    }
}
