using UnityEditor;
using UnityEngine;

public class UpgradeColliders
{
    [MenuItem("Tools/Upgrade CompositeCollider2D")]
    public static void Upgrade()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/2 Boss Stage" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;

            if (prefab != null)
            {
                EditorUtility.SetDirty(prefab);
                Debug.Log($"Updated: {path}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Done!");

    }
}