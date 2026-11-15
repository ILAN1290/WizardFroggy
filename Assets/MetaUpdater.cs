using UnityEditor;
using UnityEngine;

public class MetaUpdater
{
    [MenuItem("Tools/Update All Meta Files")]
    public static void UpdateMetas()
    {
        // Собираем все ассеты проекта
        string[] guids = AssetDatabase.FindAssets("", new[] { "Assets" });
        string[] paths = new string[guids.Length];

        for (int i = 0; i < guids.Length; i++)
            paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

        // Принудительно пересериализуем все .meta файлы
        AssetDatabase.ForceReserializeAssets(paths);
        AssetDatabase.SaveAssets();

        Debug.Log($"Обновлено {paths.Length} .meta файлов");
    }
}