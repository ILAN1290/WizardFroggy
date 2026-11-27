using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[InitializeOnLoad]
public static class TowerDefenceSceneBuilder
{
    private const string BossScenePath = "Assets/Scenes/Boss.unity";
    private const string TowerDefenceScenePath = "Assets/Scenes/TowerDefence.unity";
    private const string GrandpaSpritePath = "Assets/Sprites/дед.png";
    private const string RoadTileSetPath = "Assets/Sprites/Tile Sprites/Tileset_48x48_9.png";
    private const string RoadTileSpriteName = "Tileset_48x48_9_15";
    private const string FrogIconPath = "Assets/Sprites/UI Game/Dialogue/ЛЯГУХА Иконка.png";
    private const string GhostSpritePath = "Assets/Sprites/призрак.png";
    private const string BookSpritePath = "Assets/Sprites/книги фрогги.png";
    private const string MushroomSpritePath = "Assets/Sprites/грибочек.png";
    private const string UpgradePlusPath = "Assets/Sprites/UI Game/Merchenant/кнопочка_плюс.png";
    private const string SellMinusPath = "Assets/Sprites/UI Game/Merchenant/кнопочка_минус.png";
    private const string BuildButtonPath = "Assets/Sprites/UI Game/Merchenant/кнопочка.png";
    private const string TdaFontPath = "Assets/Fonts/TDAText SDF.asset";
    private const string TdsBookPagePath = "Assets/Sprites/UI Game/Studying/Leaflet.png";
    private const string TimerBackgroundPath = "Assets/Sprites/UI Game/Death Menu/pixil-frame-0 - 2025-08-06T220919.278.png";
    private const string GrandpaHpBackgroundPath = "Assets/Sprites/UI Game/Bars/pixil-frame-0 - 2025-08-04T212751.079.png";
    private const string KeySpritePath = "Assets/Sprites/Item/ключ 1.png";
    private const string KeyItemPath = "Assets/Prefabs/Item/TowerDefenceKey.asset";
    private const string DungeonTilesetPath = "Assets/2D Dungeon PixelArt Tileset/Dungeon-2D-PixelArt-Tileset.png";

    private static bool autoBuildQueued;

    static TowerDefenceSceneBuilder()
    {
        // Во время входа в Play Mode Unity может перезагрузить домен и повторно
        // выполнить InitializeOnLoad. В этот момент автосборку сцены запускать нельзя.
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

        if (IsEditorInEditMode())
            EditorApplication.delayCall += QueueAutoBuild;
    }

    private static bool IsEditorInEditMode()
    {
        return !EditorApplication.isPlaying &&
               !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredPlayMode ||
            state == PlayModeStateChange.ExitingPlayMode)
        {
            CancelQueuedAutoBuild();
        }
    }

    private static void CancelQueuedAutoBuild()
    {
        EditorApplication.delayCall -= QueueAutoBuild;
        EditorApplication.update -= BuildSceneWhenEditorReady;
        autoBuildQueued = false;
    }

    private static void QueueAutoBuild()
    {
        if (!IsEditorInEditMode())
            return;

        if (autoBuildQueued)
            return;

        autoBuildQueued = true;
        EditorApplication.update += BuildSceneWhenEditorReady;
    }

    private static void BuildSceneWhenEditorReady()
    {
        if (EditorApplication.isCompiling)
            return;

        // Если Unity начала переход в Play Mode, отменяем отложенную автосборку,
        // а не ждём окончания игры и не открываем сцены из редакторского API.
        if (!IsEditorInEditMode())
        {
            CancelQueuedAutoBuild();
            return;
        }

        EditorApplication.update -= BuildSceneWhenEditorReady;
        autoBuildQueued = false;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BossScenePath) == null)
        {
            Debug.LogError("Tower Defence: не найдена исходная сцена Boss.");
            return;
        }

        BuildScene(false);
    }

    [MenuItem("Wizard Froggy/Build Tower Defence Scene From Boss")]
    private static void RebuildFromMenu()
    {
        BuildScene(true);
    }

    private static void BuildScene(bool allowOverwrite)
    {
        if (!IsEditorInEditMode())
        {
            Debug.LogWarning("Tower Defence: создание сцены доступно только в Edit Mode. Остановите Play Mode и повторите попытку.");
            CancelQueuedAutoBuild();
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
                EnsureExistingSceneSystems(existingScene);
                DisableLegacyBossFlow(existingScene);
                EditorSceneManager.SaveScene(existingScene, TowerDefenceScenePath);
                if (openedExistingForCheck)
                    EditorSceneManager.CloseScene(existingScene, true);
                AddTowerDefenceToBuildSettings();
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

        GameObject candle = FindInScene(targetScene, "candle");
        if (candle != null)
            candle.SetActive(false);

        DisableLegacyBossFlow(targetScene);

        GameObject dialogueElements = FindInScene(targetScene, "DialogueElements");
        if (dialogueElements != null)
            dialogueElements.SetActive(false);

        Sprite grandpaSprite = AssetDatabase.LoadAllAssetsAtPath(GrandpaSpritePath)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == "дед_0" || sprite.name == "дед");
        Sprite frogIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FrogIconPath);
        Sprite roadTileSprite = AssetDatabase.LoadAllAssetsAtPath(RoadTileSetPath)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == RoadTileSpriteName);

        if (grandpaSprite == null)
            Debug.LogError("Не найден спрайт дедушки в Assets/Sprites/дед.png.");
        if (roadTileSprite == null)
            Debug.LogError("Не найден тайл дорожки " + RoadTileSpriteName + " в " + RoadTileSetPath);
        if (frogIcon == null)
            Debug.LogWarning("Не найдена иконка лягушки для диалога.");

        GameObject controllerObject = new GameObject("Tower Defence Scene Controller");
        SceneManager.MoveGameObjectToScene(controllerObject, targetScene);

        TowerDefenceSceneController controller =
            controllerObject.AddComponent<TowerDefenceSceneController>();

        controllerObject.AddComponent<FloorFadeTransition>();
        controllerObject.AddComponent<TowerDefenceWaveSpawner>();
        controllerObject.AddComponent<TowerDefenceManager>();
        ConfigureSceneReferences(controllerObject);

        if (FindInScene(targetScene, "Frog") == null)
            Debug.LogError("В копии Boss не найден объект Frog.");
        if (FindComponentInScene<DialogueManager>(targetScene) == null)
            Debug.LogError("В копии Boss не найден DialogueManager.");

        EditorSceneManager.SaveScene(targetScene, TowerDefenceScenePath);
        AddTowerDefenceToBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log("TowerDefence создана из Boss. Сцена добавлена в Build Settings.");
    }

    private static void DisableLegacyBossFlow(Scene scene)
    {
        GameObject gameManagerObject = FindInScene(scene, "Game Manager");
        if (gameManagerObject != null)
        {
            // Disable every legacy Boss-flow behaviour on this object. The
            // AudioSource stays enabled so shared audio assets remain available.
            foreach (MonoBehaviour behaviour in gameManagerObject.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null)
                    behaviour.enabled = false;
            }
        }

        // Стеллажи по бокам являются частью окружения библиотеки TDS.
        GameObject leftShelves = FindInScene(scene, "Left Book Cases");
        if (leftShelves != null)
            leftShelves.SetActive(true);

        GameObject rightShelves = FindInScene(scene, "Right Book Cases");
        if (rightShelves != null)
            rightShelves.SetActive(true);

        GameObject candle = FindInScene(scene, "candle");
        if (candle != null)
            candle.SetActive(false);

        GameObject greenCandlestickLeft = FindInScene(scene, "green candlestick 1");
        if (greenCandlestickLeft != null)
            greenCandlestickLeft.SetActive(false);

        GameObject greenCandlestickRight = FindInScene(scene, "green candlestick 2");
        if (greenCandlestickRight != null)
            greenCandlestickRight.SetActive(false);
    }

    private static void EnsureExistingSceneSystems(Scene scene)
    {
        GameObject controllerObject = FindInScene(
            scene, "Tower Defence Scene Controller");
        if (controllerObject == null)
        {
            Debug.LogError("Tower Defence: в существующей сцене не найден контроллер.");
            return;
        }

        if (controllerObject.GetComponent<TowerDefenceSceneController>() == null)
            controllerObject.AddComponent<TowerDefenceSceneController>();
        if (controllerObject.GetComponent<FloorFadeTransition>() == null)
            controllerObject.AddComponent<FloorFadeTransition>();
        // The manager caches the wave spawner in Awake.
        if (controllerObject.GetComponent<TowerDefenceWaveSpawner>() == null)
            controllerObject.AddComponent<TowerDefenceWaveSpawner>();
        if (controllerObject.GetComponent<TowerDefenceManager>() == null)
            controllerObject.AddComponent<TowerDefenceManager>();

        ConfigureSceneReferences(controllerObject);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureSceneReferences(GameObject controllerObject)
    {
        Sprite grandpa = LoadSprite(GrandpaSpritePath, "дед_0");
        Sprite ghost = LoadSprite(GhostSpritePath, "призрак_0");
        Sprite mushroom = LoadSprite(MushroomSpritePath, null);
        Sprite frogIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FrogIconPath);
        Sprite roadTile = LoadSprite(RoadTileSetPath, RoadTileSpriteName);
        Sprite plus = LoadSprite(UpgradePlusPath, null);
        Sprite minus = LoadSprite(SellMinusPath, null);
        Sprite buildButton = LoadSprite(BuildButtonPath, null);
        Sprite tdsBookPage = AssetDatabase.LoadAssetAtPath<Sprite>(TdsBookPagePath);
        Sprite timerBackground = AssetDatabase.LoadAssetAtPath<Sprite>(TimerBackgroundPath);
        Sprite grandpaHpBackground = AssetDatabase.LoadAssetAtPath<Sprite>(GrandpaHpBackgroundPath);
        Sprite keySprite = LoadSprite(KeySpritePath, "ключ 1_0");
        Sprite previousWallPatch = LoadSprite(
            DungeonTilesetPath, "Dungeon-2D-PixelArt-Tileset_5");
        Sprite[] closedDoorTiles =
        {
            LoadSprite(DungeonTilesetPath, "Top_Left_Dungeon_Door_Closed"),
            LoadSprite(DungeonTilesetPath, "Top_Middle_Dungeon_Door_Closed"),
            LoadSprite(DungeonTilesetPath, "Top_Right_Dungeon_Door_Closed"),
            LoadSprite(DungeonTilesetPath, "Bottom_Left_Dungeon_Door_Closed"),
            LoadSprite(DungeonTilesetPath, "Bottom_Middle_Dungeon_Door_Closed"),
            LoadSprite(DungeonTilesetPath, "Bottom_Right_Dungeon_Door_Closed")
        };
        Sprite[] openDoorTiles =
        {
            LoadSprite(DungeonTilesetPath, "Top_Left_Dungeon_Door_Opened"),
            LoadSprite(DungeonTilesetPath, "Top_Middle_Dungeon_Door_Opened"),
            LoadSprite(DungeonTilesetPath, "Top_Right_Dungeon_Door_Opened"),
            LoadSprite(DungeonTilesetPath, "Bottom_Left_Dungeon_Door_Opened"),
            LoadSprite(DungeonTilesetPath, "Bottom_Middle_Dungeon_Door_Opened"),
            LoadSprite(DungeonTilesetPath, "Bottom_Right_Dungeon_Door_Opened")
        };
        Item keyItem = AssetDatabase.LoadAssetAtPath<Item>(KeyItemPath);
        Item[] waveRewardItems =
        {
            AssetDatabase.LoadAssetAtPath<Item>("Assets/Prefabs/Item/HealingPotion.asset"),
            AssetDatabase.LoadAssetAtPath<Item>("Assets/Prefabs/Item/SpeedPotion.asset"),
            AssetDatabase.LoadAssetAtPath<Item>("Assets/Prefabs/Item/StrengthPotion.asset"),
            AssetDatabase.LoadAssetAtPath<Item>("Assets/Prefabs/Item/Bomb.asset")
        };
        TMP_FontAsset tdaFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TdaFontPath);
        AudioClip waveStart = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/Free Pack/Ghost Manifestation 1.wav");
        AudioClip waveEnd = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/ArtDesireBoutique/Audio/EXPLOSIONS/FANFARES/Fanfare (1).wav");
        AudioClip ghostHit = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/ArtDesireBoutique/Audio/EXPLOSIONS/IMPACTS/Impact (1).wav");
        AudioClip towerShot = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/Free Pack/Autocannon - Shot 1.wav");
        AudioClip cannonImpact = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/Free Pack/Explosion 1.wav");
        AudioClip purchase = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/ArtDesireBoutique/Audio/MENU/BUTTONS/button (1).wav");
        AudioClip sell = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/ArtDesireBoutique/Audio/PLAYER/INTERACTIONS/Interaction (1).wav");
        AudioClip upgrade = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/ArtDesireBoutique/Audio/POWERUPS/Powerup (1).wav");

        TowerDefenceSceneController controller =
            controllerObject.GetComponent<TowerDefenceSceneController>();
        if (controller != null)
        {
            SerializedObject serialized = new SerializedObject(controller);
            SetObject(serialized, "grandpaSpriteAsset", grandpa);
            SetObject(serialized, "roadTileSprite", roadTile);
            SetObject(serialized, "previousWallPatchSprite", previousWallPatch);
            SetSpriteArray(serialized, "closedDoorTileSprites", closedDoorTiles);
            SetSpriteArray(serialized, "openDoorTileSprites", openDoorTiles);
            SetObject(serialized, "frogIcon", frogIcon);
            SetObject(serialized, "keyPickupSprite", keySprite);
            SetObject(serialized, "keyInventoryItem", keyItem);
            SetObject(serialized, "dialogueManager",
                FindComponentInScene<DialogueManager>(controllerObject.scene));
            SerializedProperty doorSize = serialized.FindProperty("centralDoorColliderSize");
            if (doorSize != null)
                doorSize.vector2Value = new Vector2(3f, 2f);
            SerializedProperty doorPosition = serialized.FindProperty("centralDoorPosition");
            if (doorPosition != null)
                doorPosition.vector3Value = new Vector3(2.6472082f, 2.1551251f, 0.15f);
            SerializedProperty tileSize = serialized.FindProperty("doorTileSize");
            if (tileSize != null)
                tileSize.floatValue = 1f;
            SerializedProperty doorOffset = serialized.FindProperty("doorVerticalOffset");
            if (doorOffset != null)
                doorOffset.floatValue = 0f;
            SerializedProperty position = serialized.FindProperty("grandpaPosition");
            if (position != null)
                position.vector3Value = new Vector3(-6.5f, -4.5f, 0f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        TowerDefenceManager manager =
            controllerObject.GetComponent<TowerDefenceManager>();
        if (manager != null)
        {
            SerializedObject serialized = new SerializedObject(manager);
            SetObject(serialized, "whiteGhostSprite", ghost);
            SetObject(serialized, "blueGhostSprite", ghost);
            SetObject(serialized, "redGhostSprite", ghost);
            SetObject(serialized, "blackGhostSprite", ghost);
            SetObject(serialized, "bossGhostSprite", ghost);
            SetObject(serialized, "grandpaSprite", grandpa);
            SetObject(serialized, "roadTileSprite", roadTile);
            SetObject(serialized, "mushroomSprite", mushroom);
            SetObject(serialized, "instructionBookSprite", tdsBookPage);
            SetObject(serialized, "instructionButtonSprite", buildButton);
            SetObject(serialized, "timerBackgroundSprite", timerBackground);
            SetObject(serialized, "statusBackgroundSprite", timerBackground);
            SetObject(serialized, "waveCountBackgroundSprite", timerBackground);
            SetObject(serialized, "grandpaHpBackgroundSprite", grandpaHpBackground);
            SetObject(serialized, "upgradePlusButtonSprite", plus);
            SetObject(serialized, "sellMinusButtonSprite", minus);
            SetObject(serialized, "buildButtonSprite", buildButton);
            SetObject(serialized, "uiFontAsset", tdaFont);
            SetObject(serialized, "waveStartSound", waveStart);
            SetObject(serialized, "waveEndSound", waveEnd);
            SetObject(serialized, "ghostHitSound", ghostHit);
            SetObject(serialized, "towerShotSound", towerShot);
            SetObject(serialized, "cannonImpactSound", cannonImpact);
            SetObject(serialized, "purchaseSound", purchase);
            SetObject(serialized, "sellSound", sell);
            SetObject(serialized, "upgradeSound", upgrade);
            SetObjectArray(serialized, "waveRewardItems", waveRewardItems);
            SerializedProperty rewardChance = serialized.FindProperty("waveRewardDropChance");
            if (rewardChance != null)
                rewardChance.floatValue = 0.7f;
            SerializedProperty startingMushrooms = serialized.FindProperty("startingMushrooms");
            if (startingMushrooms != null)
                startingMushrooms.intValue = 5;
            SerializedProperty machineGunStats = serialized.FindProperty("machineGunStats");
            SerializedProperty machineGunRange = machineGunStats != null
                ? machineGunStats.FindPropertyRelative("range") : null;
            if (machineGunRange != null)
                machineGunRange.floatValue = 2.325f;
            SerializedProperty standardStats = serialized.FindProperty("standardStats");
            SerializedProperty standardRange = standardStats != null
                ? standardStats.FindPropertyRelative("range") : null;
            if (standardRange != null)
                standardRange.floatValue = 1.575f;
            SerializedProperty blackHealth = serialized.FindProperty("blackGhostHealth");
            if (blackHealth != null) blackHealth.floatValue = 180f;
            SerializedProperty blackSpeed = serialized.FindProperty("blackGhostSpeedMultiplier");
            if (blackSpeed != null) blackSpeed.floatValue = 0.8f;
            SerializedProperty blackSize = serialized.FindProperty("blackGhostSize");
            if (blackSize != null) blackSize.floatValue = 1.15f;
            SerializedProperty bossHealth = serialized.FindProperty("bossGhostHealth");
            if (bossHealth != null) bossHealth.floatValue = 10000f;
            SerializedProperty bossSpeed = serialized.FindProperty("bossGhostSpeed");
            if (bossSpeed != null) bossSpeed.floatValue = 0.33f;
            SerializedProperty bossSize = serialized.FindProperty("bossGhostSize");
            if (bossSize != null) bossSize.floatValue = 2.2f;

            string[] phrases =
            {
                "НЕ ШУМИТЕ, ПРОКАЗНИКИ!",
                "Шапки! ШАПКИ надевайте!",
                "Расшумелись...",
                "Оххх, спинааа...",
                "Ла-ла-ла-лаААААА",
                "Ой, куда ж делся... Куда же он...",
                "Ой, ой-ой-ой!",
                "Непорядок...",
                "Иже иси на небеси...",
                "Ох помру я так",
                "Ломайте, ломайте! Анархия - мать порядка!"
            };
            SerializedProperty phraseArray = serialized.FindProperty("grandpaPhrases");
            if (phraseArray != null)
            {
                phraseArray.arraySize = phrases.Length;
                for (int i = 0; i < phrases.Length; i++)
                    phraseArray.GetArrayElementAtIndex(i).stringValue = phrases[i];
            }

            SerializedProperty cannonStats = serialized.FindProperty("cannonStats");
            SerializedProperty cannonInterval = cannonStats != null
                ? cannonStats.FindPropertyRelative("attackInterval") : null;
            if (cannonInterval != null)
                cannonInterval.floatValue = 1.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        TowerDefenceWaveSpawner spawner =
            controllerObject.GetComponent<TowerDefenceWaveSpawner>();
        if (spawner != null)
        {
            SerializedObject serialized = new SerializedObject(spawner);
            SerializedProperty waves = serialized.FindProperty("waves");
            SetBlackGhostCount(waves, 4, 1);
            SetBlackGhostCount(waves, 9, 2);
            SetBlackGhostCount(waves, 14, 3);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        if (tdaFont != null)
        {
            foreach (GameObject root in controllerObject.scene.GetRootGameObjects())
            {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                    text.font = tdaFont;
            }
        }

        if (grandpa == null)
            Debug.LogError("Tower Defence: не найден спрайт дедушки.");
        if (ghost == null)
            Debug.LogError("Tower Defence: не найден спрайт призрака.");
        if (roadTile == null)
            Debug.LogError("Tower Defence: не найден тайл дорожки.");
        if (buildButton == null)
            Debug.LogError("Tower Defence: не найден спрайт кнопки строительства.");
        if (tdaFont == null)
            Debug.LogError("Tower Defence: не найден шрифт " + TdaFontPath);
        if (buildButton == null)
            Debug.LogWarning("Tower Defence: не найдена текстура кнопки обучения.");
        if (tdsBookPage == null)
            Debug.LogWarning("Tower Defence: не найден лист бумаги для обучения.");
        if (timerBackground == null || grandpaHpBackground == null)
            Debug.LogWarning("Tower Defence: не найдены текстуры панелей HUD.");
        if (keySprite == null || keyItem == null)
            Debug.LogWarning("Tower Defence: не найдены спрайт или предмет ключа.");
        if (previousWallPatch == null)
            Debug.LogWarning("Tower Defence: не найден тайл для закрытия старого проёма.");
        if (closedDoorTiles.Any(sprite => sprite == null) ||
            openDoorTiles.Any(sprite => sprite == null))
            Debug.LogWarning("Tower Defence: не все спрайты двери найдены в Dungeon-2D-PixelArt-Tileset.");
    }

    private static Sprite LoadSprite(string path, string preferredName)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .ToArray();

        if (!string.IsNullOrEmpty(preferredName))
        {
            Sprite preferred = sprites.FirstOrDefault(
                sprite => sprite.name == preferredName);
            if (preferred != null)
                return preferred;
        }

        return sprites.Length > 0 ? sprites[0] : null;
    }

    private static void SetBlackGhostCount(
        SerializedProperty waves,
        int waveIndex,
        int count)
    {
        if (waves == null || !waves.isArray ||
            waveIndex < 0 || waveIndex >= waves.arraySize)
            return;

        SerializedProperty wave = waves.GetArrayElementAtIndex(waveIndex);
        SerializedProperty blackGhosts = wave.FindPropertyRelative("blackGhosts");
        if (blackGhosts != null)
            blackGhosts.intValue = count;
    }

    private static void SetObjectArray(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object[] values)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray || values == null)
            return;

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetSpriteArray(
        SerializedObject serialized,
        string propertyName,
        Sprite[] sprites)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray || sprites == null)
            return;

        property.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    private static void SetObject(
        SerializedObject serialized,
        string propertyName,
        Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
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
