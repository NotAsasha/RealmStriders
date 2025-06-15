using UnityEditor;
using UnityEngine;

public class FindMissingReferences
{
    [MenuItem("Tools/Find Missing References in Scene")]
    public static void FindMissing()
    {
        var allObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (var go in allObjects)
        {
            var components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    Debug.LogWarning($"Missing script in GameObject: {go.name}", go);
                }
            }
        }
        Debug.LogWarning($"End Search");
    }
}
