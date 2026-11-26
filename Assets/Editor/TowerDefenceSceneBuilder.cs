using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TowerDefenceSceneBuilder
{
    private const string BossScenePath = "Assets/Scenes/Boss.unity";
    private const string TowerDefenceScenePath = "Assets/Scenes/TowerDefence.unity";
    private const string GrandpaSheetPath =
        "Assets/Sprites/Modern tiles_Free/Characters_free/RPGMAKERMV/Characters_MV.png";
    private const string FrogIconPath = "Assets/Sprites/UI Game/Dialogue/ЛЯГУХА Иконка.png";

    static TowerDefenceSceneBuilder()
    {
        // Создаём сцену автоматически при первом открытии проекта с этой веткой.
        EditorApplication.delayCall += BuildSceneIfMissing;
    }

    private static void BuildSceneIfMissing()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TowerDefenceScenePath) != null)
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BossScenePath) == null)
            return;

        BuildScene(false);
    }

    [MenuItem("Wizard Froggy/Build Tower Defence Scene From Boss")]
    private static void RebuildFromMenu()
    {
        BuildScene(true);
    }

    private static void BuildScene(bool allowOverwrite)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Закройте Play Mode перед созданием TowerDefence.");
            return;
        }

        SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(TowerDefenceScenePath);
        if (existing != null)
        {
            if (!allowOverwrite)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Пересоздать TowerDefence?",
                "Сцена TowerDefence будет заново скопирована из Boss. Текущие ручные изменения сцены будут потеряны.",
                "Пересоздать",
                "Отмена");

            if (!confirmed)
                return;

            AssetDatabase.DeleteAsset(TowerDefenceScenePath);
        }

        Scene sourceScene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Additive);
        if (!sourceScene.IsValid() || !sourceScene.isLoaded)
        {
            Debug.LogError("Не удалось открыть Assets/Scenes/Boss.unity.");
            return;
        }

        Scene targetScene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Additive);

        EditorSceneManager.SetActiveScene(targetScene);

        // Переносим все корневые объекты целиком, сохраняя внутренние ссылки сцены Boss.
        foreach (GameObject root in sourceScene.GetRootGameObjects())
            SceneManager.MoveGameObjectToScene(root, targetScene);

        EditorSceneManager.CloseScene(sourceScene, false);

        string[] bossOnlyObjects =
        {
            "Mops",
            "Chih",
            "Bul",
            "Hellhound",
            "BodyAndHeads",
            "Wind",
            "Tornado",
            "FireAboveHead",
            "Green Fire",
            "Portal",
            "Muzzle",
            "Healing",
            "Intro(Cutscene)"
        };

        foreach (string objectName in bossOnlyObjects)
        {
            GameObject bossObject = FindInScene(targetScene, objectName);
            if (bossObject != null)
                bossObject.SetActive(false);
        }

        GameObject gameManagerObject = FindInScene(targetScene, "Game Manager");
        if (gameManagerObject != null)
        {
            foreach (MonoBehaviour behaviour in gameManagerObject.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType().Name == "GameManager")
                    behaviour.enabled = false;
            }
        }

        GameObject dialogueElements = FindInScene(targetScene, "DialogueElements");
        if (dialogueElements != null)
            dialogueElements.SetActive(false);

        Texture2D grandpaSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(GrandpaSheetPath);
        Sprite frogIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FrogIconPath);

        if (grandpaSheet == null)
            Debug.LogError("Не найден спрайт-лист Characters_MV.png для дедушки.");
        if (frogIcon == null)
            Debug.LogWarning("Не найдена иконка лягушки для диалога.");

        GameObject controllerObject = new GameObject("Tower Defence Scene Controller");
        SceneManager.MoveGameObjectToScene(controllerObject, targetScene);

        TowerDefenceSceneController controller =
            controllerObject.AddComponent<TowerDefenceSceneController>();

        controllerObject.AddComponent<FloorFadeTransition>();

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("grandpaSheet").objectReferenceValue = grandpaSheet;
        serializedController.FindProperty("frogIcon").objectReferenceValue = frogIcon;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        if (FindInScene(targetScene, "Frog") == null)
            Debug.LogError("В копии Boss не найден объект Frog.");
        if (FindComponentInScene<DialogueManager>(targetScene) == null)
            Debug.LogError("В копии Boss не найден DialogueManager.");

        EditorSceneManager.SaveScene(targetScene, TowerDefenceScenePath);
        AddTowerDefenceToBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log("TowerDefence создана из Boss. Сцена добавлена в Build Settings.");
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == objectName)
                    return item.gameObject;
            }
        }

        return null;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static void AddTowerDefenceToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        var updatedScenes = scenes.ToList();
        int existingIndex = updatedScenes.FindIndex(scene => scene.path == TowerDefenceScenePath);

        if (existingIndex >= 0)
            updatedScenes[existingIndex] = new EditorBuildSettingsScene(TowerDefenceScenePath, true);
        else
            updatedScenes.Add(new EditorBuildSettingsScene(TowerDefenceScenePath, true));

        EditorBuildSettings.scenes = updatedScenes.ToArray();
    }
}
