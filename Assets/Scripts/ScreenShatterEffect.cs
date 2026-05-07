using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ScreenShatterEffect : MonoBehaviour
{
    public Material shatterMaterial;
    public int shardCount = 12;
    public float animationDuration = 0.8f;
    public float explosionForce = 1000f;

    private List<GameObject> shards = new List<GameObject>();
    private Texture2D screenshot;

    public void TriggerEffect(System.Action onCaptured, System.Action onComplete = null)
    {
        StartCoroutine(ExecuteEffect(onCaptured, onComplete));
    }

    private IEnumerator ExecuteEffect(System.Action onCaptured, System.Action onComplete)
    {
        // 1. Capture Screen
        yield return new WaitForEndOfFrame();
        
        screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        if (screenshot == null)
        {
            onCaptured?.Invoke();
            yield break;
        }

        screenshot.filterMode = FilterMode.Bilinear;
        int captureW = screenshot.width;
        int captureH = screenshot.height;

        // 2. Generate Shards BEFORE notifying (so they are ready when menu shows)
        GenerateSplitShards(captureW, captureH);

        // 3. Notify that capture is done (PauseMenu content becomes active behind shards)
        onCaptured?.Invoke();

        // 4. Animate to final positions
        float elapsed = 0;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Smooth opening
            float ease = t * t * (3f - 2f * t); 

            foreach (var shard in shards)
            {
                if (shard == null) continue;
                var info = shard.GetComponent<ShardInfo>();
                shard.transform.localPosition = (Vector3)(info.targetOffset * ease);
                shard.transform.localRotation = Quaternion.Euler(0, 0, info.targetRotation * ease);
            }
            yield return null;
        }

        onComplete?.Invoke();
    }

    private void GenerateSplitShards(int width, int height)
    {
        ClearShards();

        float midX = width / 2f;
        float midY = height / 2f;

        // Quadrants: Bottom-Left, Bottom-Right, Top-Right, Top-Left
        // direction: used for targetOffset
        
        // Q1: Bottom-Left
        CreateShard(new Vector2[] { 
            new Vector2(0, 0), 
            new Vector2(midX, 0), 
            new Vector2(midX, midY), 
            new Vector2(0, midY) 
        }, width, height, new Vector2(-0.6f, -0.6f));

        // Q2: Bottom-Right
        CreateShard(new Vector2[] { 
            new Vector2(midX, 0), 
            new Vector2(width, 0), 
            new Vector2(width, midY), 
            new Vector2(midX, midY) 
        }, width, height, new Vector2(0.6f, -0.6f));

        // Q3: Top-Right
        CreateShard(new Vector2[] { 
            new Vector2(midX, midY), 
            new Vector2(width, midY), 
            new Vector2(width, height), 
            new Vector2(midX, height) 
        }, width, height, new Vector2(0.6f, 0.6f));

        // Q4: Top-Left
        CreateShard(new Vector2[] { 
            new Vector2(0, midY), 
            new Vector2(midX, midY), 
            new Vector2(midX, height), 
            new Vector2(0, height) 
        }, width, height, new Vector2(-0.6f, 0.6f));
    }

    private void CreateShard(Vector2[] poly, int width, int height, Vector2 direction)
    {
        GameObject shardObj = new GameObject("Shard", typeof(RectTransform));
        shardObj.transform.SetParent(this.transform, false);
        
        RectTransform rt = shardObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        
        shardObj.AddComponent<CanvasRenderer>();
        var shard = shardObj.AddComponent<ShardGraphic>();
        shard.color = Color.white;
        shard.raycastTarget = false;
        shard.texture = screenshot;
        shard.material = shatterMaterial; 
        
        shard.vertices = poly;
        shard.refWidth = width;
        shard.refHeight = height;

        shard.uvs = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            shard.uvs[i] = new Vector2(poly[i].x / width, poly[i].y / height);
        }
        shard.SetAllDirty();

        var info = shardObj.AddComponent<ShardInfo>();
        // Move towards corners
        info.targetOffset = new Vector2(direction.x * width, direction.y * height);
        info.targetRotation = direction.x * direction.y * 10f;

        shards.Add(shardObj);
    }

    public void ResetEffect()
    {
        ClearShards();
        if (screenshot != null) Destroy(screenshot);
    }

    private void ClearShards()
    {
        foreach (var shard in shards)
        {
            if (shard != null) Destroy(shard);
        }
        shards.Clear();
    }

    public class ShardInfo : MonoBehaviour
    {
        public Vector2 targetOffset;
        public float targetRotation;
    }

    public class ShardGraphic : MaskableGraphic
    {
        public Texture texture;
        public Vector2[] vertices;
        public Vector2[] uvs;
        public float refWidth;
        public float refHeight;

        public override Texture mainTexture => texture != null ? texture : base.mainTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (vertices == null || vertices.Length < 3 || refWidth <= 0 || refHeight <= 0) return;

            float w = rectTransform.rect.width;
            float h = rectTransform.rect.height;

            for (int i = 0; i < vertices.Length; i++)
            {
                // Correct mapping from reference (capture) space to UI Rect space
                float vx = (vertices[i].x / refWidth) * w - w / 2f;
                float vy = (vertices[i].y / refHeight) * h - h / 2f;
                vh.AddVert(new Vector3(vx, vy, 0), color, uvs[i]);
            }

            for (int i = 0; i < vertices.Length - 2; i++)
            {
                vh.AddTriangle(0, i + 1, i + 2);
            }
        }
    }
}
