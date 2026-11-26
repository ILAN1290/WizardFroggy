using System.Collections.Generic;
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
    private const string GrandpaSpritePath = "Assets/Sprites/дед.png";
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

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BossScenePath) == null)
            return;

        // Если в репозитории уже лежит пустая заготовка TowerDefence,
        // автоматически заполняем её копией Boss при первой загрузке проекта.
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

        SceneAsset existingAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TowerDefenceScenePath);
        Scene existingScene = SceneManager.GetSceneByPath(TowerDefenceScenePath);
        bool openedExistingForCheck = false;
        bool existingIsEmpty = true;

        if (existingAsset != null)
        {
            if (!existingScene.IsValid() || !existingScene.isLoaded)
            {
                existingScene = EditorSceneManager.OpenScene(TowerDefenceScenePath, OpenSceneMode.Additive);
                openedExistingForCheck = true;
            }

            existingIsEmpty = existingScene.GetRootGameObjects().Length == 0;

            if (!allowOverwrite && !existingIsEmpty)
            {
                if (openedExistingForCheck)
                    EditorSceneManager.CloseScene(existingScene, true);
                return;
            }

            if (allowOverwrite)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Пересоздать TowerDefence?",
                    "Сцена TowerDefence будет заново скопирована из Boss. Текущие ручные изменения сцены будут потеряны.",
                    "Пересоздать",
                    "Отмена");

                if (!confirmed)
                {
                    if (openedExistingForCheck)
                        EditorSceneManager.CloseScene(existingScene, true);
                    return;
                }
            }

            if (!existingIsEmpty)
            {
                EditorSceneManager.CloseScene(existingScene, true);
                existingScene = default(Scene);
            }
        }

        Scene sourceScene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Additive);
        if (!sourceScene.IsValid() || !sourceScene.isLoaded)
        {
            Debug.LogError("Не удалось открыть Assets/Scenes/Boss.unity.");
            return;
        }

        // Переиспользуем уже открытую пустую сцену, чтобы сохранить её GUID и метафайл.
        Scene targetScene = existingAsset != null && existingIsEmpty &&
                            existingScene.IsValid() && existingScene.isLoaded
            ? existingScene
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

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

        Sprite grandpaSprite = AssetDatabase.LoadAllAssetsAtPath(GrandpaSpritePath)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == "дед_0" || sprite.name == "дед");
        Sprite frogIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FrogIconPath);

        if (grandpaSprite == null)
            Debug.LogError("Не найден спрайт дедушки в Assets/Sprites/дед.png.");
        if (frogIcon == null)
            Debug.LogWarning("Не найдена иконка лягушки для диалога.");

        GameObject controllerObject = new GameObject("Tower Defence Scene Controller");
        SceneManager.MoveGameObjectToScene(controllerObject, targetScene);

        TowerDefenceSceneController controller =
            controllerObject.AddComponent<TowerDefenceSceneController>();

        controllerObject.AddComponent<FloorFadeTransition>();

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("grandpaSpriteAsset").objectReferenceValue = grandpaSprite;
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
        var updatedScenes = new List<EditorBuildSettingsScene>();
        bool towerDefenceAdded = false;

        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == TowerDefenceScenePath)
            {
                // В Build Settings не должно оставаться нескольких ссылок на один путь
                // с разными GUID (они могли попасть туда из main и TowerDefence).
                if (!towerDefenceAdded)
                {
                    updatedScenes.Add(new EditorBuildSettingsScene(TowerDefenceScenePath, true));
                    towerDefenceAdded = true;
                }
                continue;
            }

            updatedScenes.Add(scene);
        }

        if (!towerDefenceAdded)
            updatedScenes.Add(new EditorBuildSettingsScene(TowerDefenceScenePath, true));

        EditorBuildSettings.scenes = updatedScenes.ToArray();
    }
}
