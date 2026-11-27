using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class TowerDefenceSceneController : MonoBehaviour
{
    [Header("Персонажи")]
    [SerializeField] private Sprite grandpaSpriteAsset;
    [SerializeField] private Sprite roadTileSprite;
    [SerializeField] private Sprite[] closedDoorTileSprites = new Sprite[6];
    [SerializeField] private Sprite[] openDoorTileSprites = new Sprite[6];
    [SerializeField] private Sprite previousWallPatchSprite;
    [SerializeField] private Sprite keyPickupSprite;
    [SerializeField] private Item keyInventoryItem;
    [SerializeField] private Sprite frogIcon;

    [Header("Положение персонажей")]
    [SerializeField] private Vector3 grandpaPosition = new Vector3(-7.5f, -3.5f, 0f);
    [SerializeField] private float frogStartX = 10.5f;
    [SerializeField] private float frogWalkSpeed = 3.8f;
    [Header("Положение двери")]
    [Tooltip("Мировые координаты центра двери. Меняй X/Y здесь — позиция не будет перезаписываться при запуске сцены.")]
    [SerializeField] private Vector3 centralDoorPosition = new Vector3(0f, 3.15f, 0.15f);
    [SerializeField] private Vector2 centralDoorColliderSize = new Vector2(3f, 2f);
    [SerializeField, Min(0.1f)] private float doorTileSize = 1f;
    [SerializeField, Min(0f)] private float doorVerticalOffset = 0.06f;
    [SerializeField, Min(0.5f)] private float doorInteractDistance = 1.5f;

    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private AudioClip dialogueVoiceClip;
    private Transform frog;
    private Camera sceneCamera;
    private GameObject grandpaObject;
    private GameObject centralDoorObject;
    private SpriteRenderer[] centralDoorTileRenderers;
    private Collider2D centralDoorCollider;
    private Tile runtimeRoadTile;
    private Tile runtimeRoadStartTile;
    private bool doorOpened;
    private bool doorTransitionStarted;
    private bool postWaveSequenceStarted;
    private TowerDefenceManager gameManager;
    private readonly HashSet<Vector3Int> generatedRoadCells = new HashSet<Vector3Int>();
    private Vector3[] routeA;
    private Vector3[] routeB;
    private readonly Vector3 spawnPosition = new Vector3(7.5f, 0.5f, 0f);

    private void Awake()
    {
        Time.timeScale = 1f;
        dialogueManager = FindDialogueManagerInScene();

        Transform frogTransform = FindSceneTransform("Frog");
        if (frogTransform != null)
            frog = frogTransform;

        sceneCamera = Camera.main;
    }

    private float GetPixelWorldSize(float pixelCount)
    {
        Camera camera = sceneCamera != null ? sceneCamera : Camera.main;
        if (camera == null || !camera.orthographic)
            return pixelCount / 100f;

        float pixelHeight = camera.pixelHeight > 0
            ? camera.pixelHeight
            : Mathf.Max(1f, Screen.height);
        float pixelsPerWorldUnit = pixelHeight /
            Mathf.Max(0.01f, camera.orthographicSize * 2f);
        return pixelCount / Mathf.Max(1f, pixelsPerWorldUnit);
    }

    private void SetFrogSortingOrder()
    {
        if (frog == null)
            return;

        SpriteRenderer[] renderers =
            frog.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].sortingLayerName = "Default";
            renderers[i].sortingOrder = 40;
        }
    }

    private DialogueManager FindDialogueManagerInScene()
    {
        DialogueManager[] candidates =
            Resources.FindObjectsOfTypeAll<DialogueManager>();
        for (int i = 0; i < candidates.Length; i++)
        {
            DialogueManager candidate = candidates[i];
            if (candidate != null && candidate.gameObject.scene == gameObject.scene)
                return candidate;
        }

        return null;
    }

    private Transform FindSceneTransform(string targetName)
    {
        Transform[] candidates = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i];
            if (candidate != null &&
                candidate.gameObject.scene == gameObject.scene &&
                candidate.name == targetName)
                return candidate;
        }

        return null;
    }

    private IEnumerator Start()
    {
        // Дожидаемся завершения Awake у остальных объектов сцены.
        yield return null;

        if (dialogueManager == null)
            dialogueManager = FindDialogueManagerInScene();
        if (frog == null)
            frog = FindSceneTransform("Frog");
        if (sceneCamera == null)
            sceneCamera = Camera.main;
        SetFrogSortingOrder();

        CreateRoad();
        CreateCenterDoor();
        CreateGrandpa();

        // Add the spawner first: TowerDefenceManager caches it in Awake.
        if (GetComponent<TowerDefenceWaveSpawner>() == null)
            gameObject.AddComponent<TowerDefenceWaveSpawner>();

        gameManager = GetComponent<TowerDefenceManager>();
        if (gameManager == null)
            gameManager = gameObject.AddComponent<TowerDefenceManager>();

        if (grandpaObject != null)
            gameManager.SetGrandpa(grandpaObject.transform);
        gameManager.ConfigureRoutes(
            routeA, routeB, generatedRoadCells, spawnPosition);
        gameManager.AllWavesDefeated += HandleAllWavesDefeated;

        ConfigureIntroDialogue();

        if (dialogueManager == null || frog == null)
        {
            Debug.LogError(
                "Tower Defence: не удалось запустить катсцену. " +
                "Проверьте наличие активных Frog и DialogueManager в сцене.");
            yield break;
        }

        dialogueManager.OnLineDisplayed += HandleLineDisplayed;

        FloorFadeTransition fade = GetComponent<FloorFadeTransition>();
        if (fade != null)
            yield return fade.FadeFromBlack(0.55f);

        yield return PlayIntroCutscene();
    }

    private void OnDestroy()
    {
        if (dialogueManager != null)
            dialogueManager.OnLineDisplayed -= HandleLineDisplayed;
        if (gameManager != null)
            gameManager.AllWavesDefeated -= HandleAllWavesDefeated;

        if (runtimeRoadTile != null)
            Destroy(runtimeRoadTile);
        if (runtimeRoadStartTile != null)
            Destroy(runtimeRoadStartTile);
    }

    private void CreateRoad()
    {
        if (roadTileSprite == null)
        {
            Debug.LogError("Tower Defence: не назначен тайл дорожки Tileset_48x48_9_15.");
            return;
        }

        Transform previousPath = transform.Find("Tower Defence Paths");
        if (previousPath != null)
            Destroy(previousPath.gameObject);

        generatedRoadCells.Clear();

        // Длинная змейка по всей доступной площади комнаты.
        // Все отрезки строго горизонтальные или вертикальные.
        Vector3[] route =
        {
            spawnPosition,
            new Vector3(-7.5f, 0.5f, 0f),
            new Vector3(-7.5f, -1.5f, 0f),
            new Vector3(7.5f, -1.5f, 0f),
            new Vector3(7.5f, -3.5f, 0f),
            grandpaPosition
        };

        // Обе ветки используют длинный маршрут, чтобы каждый призрак
        // проходил всю дорожку, а не срезал путь к дедушке.
        routeA = (Vector3[])route.Clone();
        routeB = (Vector3[])route.Clone();
        AddRoadPolyline(generatedRoadCells, routeA);

        GameObject pathRoot = new GameObject("Tower Defence Paths");
        pathRoot.transform.SetParent(transform, false);

        Grid grid = pathRoot.AddComponent<Grid>();
        grid.cellSize = Vector3.one;
        grid.cellGap = Vector3.zero;

        Tilemap tilemap = pathRoot.AddComponent<Tilemap>();
        tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        TilemapRenderer renderer = pathRoot.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = -6;

        runtimeRoadTile = ScriptableObject.CreateInstance<Tile>();
        runtimeRoadTile.name = "Runtime Tower Defence Road Tile";
        runtimeRoadTile.sprite = roadTileSprite;
        runtimeRoadTile.colliderType = Tile.ColliderType.None;

        foreach (Vector3Int cell in generatedRoadCells)
            tilemap.SetTile(cell, runtimeRoadTile);

        // Tint the first path tile red so the ghost spawn point is obvious.
        runtimeRoadStartTile = ScriptableObject.CreateInstance<Tile>();
        runtimeRoadStartTile.name = "Runtime Tower Defence Road Start";
        runtimeRoadStartTile.sprite = roadTileSprite;
        runtimeRoadStartTile.color = new Color(1f, 0.22f, 0.22f, 1f);
        runtimeRoadStartTile.colliderType = Tile.ColliderType.None;
        tilemap.SetTile(Vector3Int.FloorToInt(spawnPosition), runtimeRoadStartTile);
    }

    private static void AddRoadPolyline(HashSet<Vector3Int> cells, Vector3[] points)
    {
        if (cells == null || points == null || points.Length < 2)
            return;

        for (int segment = 0; segment < points.Length - 1; segment++)
        {
            int x0 = Mathf.FloorToInt(points[segment].x);
            int y0 = Mathf.FloorToInt(points[segment].y);
            int x1 = Mathf.FloorToInt(points[segment + 1].x);
            int y1 = Mathf.FloorToInt(points[segment + 1].y);

            if (x0 != x1 && y0 != y1)
            {
                Debug.LogError("Tower Defence: маршрут должен состоять только из прямых участков под 90 градусов.");
                continue;
            }

            if (x0 == x1)
            {
                int minY = Mathf.Min(y0, y1);
                int maxY = Mathf.Max(y0, y1);
                for (int y = minY; y <= maxY; y++)
                    cells.Add(new Vector3Int(x0, y, 0));
            }
            else
            {
                int minX = Mathf.Min(x0, x1);
                int maxX = Mathf.Max(x0, x1);
                for (int x = minX; x <= maxX; x++)
                    cells.Add(new Vector3Int(x, y0, 0));
            }
        }
    }

    private void CreateCenterDoor()
    {
        Transform existingDoor = transform.Find("Central Door");
        if (existingDoor != null)
            Destroy(existingDoor.gameObject);

        Transform oldWall = transform.Find("Tower Defence Door Wall");
        if (oldWall != null)
            Destroy(oldWall.gameObject);
        Transform oldPatch = transform.Find("Tower Defence Old Wall Patch");
        if (oldPatch != null)
            Destroy(oldPatch.gameObject);
        Transform oldLeft = transform.Find("Upper Wall Left");
        if (oldLeft != null)
            Destroy(oldLeft.gameObject);
        Transform oldRight = transform.Find("Upper Wall Right");
        if (oldRight != null)
            Destroy(oldRight.gameObject);

        Bounds wallBounds = GetAndOpenUpperWallBounds();

        // The door uses its serialized Inspector position. Upper Limit is used
        // only for wall bounds; it must never overwrite the designer's X/Y.
        Vector3 doorWorldPosition = centralDoorPosition +
            Vector3.up * doorVerticalOffset;

        // Leave an opening in the wall collision at the Inspector-controlled X.
        BuildDoorSideColliders(wallBounds);

        centralDoorObject = new GameObject("Central Door");
        centralDoorObject.transform.SetParent(transform, false);
        centralDoorObject.transform.position = doorWorldPosition;
        centralDoorTileRenderers = new SpriteRenderer[6];

        for (int index = 0; index < centralDoorTileRenderers.Length; index++)
        {
            int row = index / 3;
            int column = index % 3;
            GameObject tileObject = new GameObject("Door Tile " + index);
            tileObject.transform.SetParent(centralDoorObject.transform, false);
            tileObject.transform.localPosition = new Vector3(
                (column - 1) * doorTileSize,
                (0.5f - row) * doorTileSize,
                0f);

            SpriteRenderer renderer = tileObject.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 39;
            centralDoorTileRenderers[index] = renderer;
        }

        ApplyDoorTileSprites(closedDoorTileSprites);

        // A non-trigger collider keeps the frog outside while the door is shut.
        // It is disabled only after the key is used in the existing door flow.
        BoxCollider2D doorCollider = centralDoorObject.AddComponent<BoxCollider2D>();
        doorCollider.size = centralDoorColliderSize;
        doorCollider.offset = Vector2.zero;
        doorCollider.isTrigger = false;
        centralDoorCollider = doorCollider;

        CreatePreviousWallPatch();
    }

    private Bounds GetAndOpenUpperWallBounds()
    {
        Transform upperLimit = FindSceneTransform("Upper Limit");
        BoxCollider2D upperCollider = upperLimit != null
            ? upperLimit.GetComponent<BoxCollider2D>()
            : null;

        if (upperCollider != null)
        {
            Bounds bounds = upperCollider.bounds;
            upperCollider.enabled = false;
            return bounds;
        }

        return new Bounds(
            new Vector3(2.6472082f, 2.1551251f, -0.01917313f),
            new Vector3(13.121123f, 0.33929998f, 0.1f));
    }

    private void BuildDoorSideColliders(Bounds wallBounds)
    {
        float halfOpening = centralDoorColliderSize.x * 0.5f;
        float openingLeft = centralDoorPosition.x - halfOpening;
        float openingRight = centralDoorPosition.x + halfOpening;

        CreateWallCollider(
            "Upper Wall Left",
            wallBounds.min.x,
            Mathf.Min(openingLeft, wallBounds.max.x),
            wallBounds);
        CreateWallCollider(
            "Upper Wall Right",
            Mathf.Max(openingRight, wallBounds.min.x),
            wallBounds.max.x,
            wallBounds);
    }

    private void CreateWallCollider(
        string objectName,
        float minX,
        float maxX,
        Bounds sourceBounds)
    {
        float width = maxX - minX;
        if (width <= 0.02f)
            return;

        GameObject wallObject = new GameObject(objectName);
        wallObject.transform.SetParent(transform, false);
        wallObject.transform.position = new Vector3(
            (minX + maxX) * 0.5f,
            sourceBounds.center.y,
            sourceBounds.center.z);

        BoxCollider2D collider = wallObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(width, sourceBounds.size.y);
        collider.isTrigger = false;
    }

    private void CreatePreviousWallPatch()
    {
        if (previousWallPatchSprite == null)
        {
            Debug.LogError("Tower Defence: не назначен тайл Dungeon-2D-PixelArt-Tileset_5.");
            return;
        }

        GameObject patchRoot = new GameObject("Tower Defence Old Wall Patch");
        patchRoot.transform.SetParent(transform, false);

        // Вырез в кирпичной стене на скриншоте занимает 3x2 клетки
        // справа от двери. Раньше заплатка ставилась за границей комнаты.
        const float firstTileX = 5.5f;
        const float firstTileY = 2.5f;
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                CreateTileVisual(
                    patchRoot.transform,
                    "Old Wall Hole Patch " + row + "_" + column,
                    previousWallPatchSprite,
                    new Vector3(
                        firstTileX + column * doorTileSize,
                        firstTileY + row * doorTileSize,
                        0.08f),
                    38);
            }
        }
    }

    private void ApplyDoorTileSprites(Sprite[] sprites)
    {
        if (centralDoorTileRenderers == null)
            return;

        for (int index = 0; index < centralDoorTileRenderers.Length; index++)
        {
            SpriteRenderer renderer = centralDoorTileRenderers[index];
            if (renderer == null)
                continue;

            Sprite sprite = sprites != null && index < sprites.Length
                ? sprites[index]
                : null;
            renderer.sprite = sprite;
            renderer.enabled = sprite != null;
        }
    }

    private void CreateTileVisual(
        Transform parent,
        string objectName,
        Sprite sprite,
        Vector3 localPosition,
        int sortingOrder)
    {
        if (sprite == null)
            return;

        GameObject tileObject = new GameObject(objectName);
        tileObject.transform.SetParent(parent, false);
        tileObject.transform.localPosition = localPosition;

        SpriteRenderer renderer = tileObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;

        Vector2 spriteSize = sprite.bounds.size;
        tileObject.transform.localScale = new Vector3(
            doorTileSize / Mathf.Max(0.001f, spriteSize.x),
            doorTileSize / Mathf.Max(0.001f, spriteSize.y),
            1f);
    }

    private void CreateGrandpa()
    {
        if (grandpaObject != null)
            return;

        if (grandpaSpriteAsset == null)
        {
            Debug.LogError("Tower Defence: не назначен спрайт Assets/Sprites/дед.png.");
            return;
        }

        grandpaObject = new GameObject("Grandpa");
        grandpaObject.transform.SetParent(transform, false);
        grandpaObject.transform.position = grandpaPosition;

        // Дедушка стоит в конце обеих веток, в нижнем левом углу арены.
        grandpaObject.transform.localScale = Vector3.one * 0.06f;

        SpriteRenderer renderer = grandpaObject.AddComponent<SpriteRenderer>();
        renderer.sprite = grandpaSpriteAsset;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 4;
    }

    private void ConfigureIntroDialogue()
    {
        if (dialogueManager == null)
        {
            Debug.LogError("Tower Defence: DialogueManager не найден в сцене.");
            return;
        }

        GameObject frogObject = frog != null ? frog.gameObject : null;
        Sprite playerIcon = frogIcon;

        if (playerIcon == null && frogObject != null)
        {
            SpriteRenderer frogRenderer = frogObject.GetComponent<SpriteRenderer>();
            if (frogRenderer != null)
                playerIcon = frogRenderer.sprite;
        }

        Sprite oldManIcon = grandpaSpriteAsset != null ? grandpaSpriteAsset : playerIcon;

        List<DialogueLine> lines = new List<DialogueLine>
        {
            MakeLine(frogObject, "Лягушка", playerIcon, "ква-ква-ква"),
            MakeLine(grandpaObject, "Дед", oldManIcon, "Кхе-кхе... Чтоооо молвишь? Сынок, не слышу!"),
            MakeLine(frogObject, "Лягушка", playerIcon, "КВА-КВА-КВА"),
            MakeLine(grandpaObject, "Дед", oldManIcon, "Родненький, не слышу совсем!"),
            MakeLine(frogObject, "Лягушка", playerIcon, "КВА-КВА-КВА!!!!!!!!!"),
            MakeLine(
                grandpaObject,
                "Дед",
                oldManIcon,
                "Аааа, милок, конечно я тебе помогу! Моя женушка всегда говорила будь добрым и стрекозы будут петь в 4 утра! Но вы разве послушаете, какое поколение пошло, ни дать, ни взять совсем бесто..."),
            MakeLine(frogObject, "Лягушка", playerIcon, "?!"),
            MakeLine(grandpaObject, "Дед", oldManIcon, "ПРИЗРАКИ, ОНИ ИДУТ!")
        };

        dialogueManager.dialogues.Clear();
        dialogueManager.dialogues.Add(new Dialogue { dialogueLines = lines });
    }

    private DialogueLine MakeLine(GameObject person, string personName, Sprite icon, string text)
    {
        return new DialogueLine
        {
            person = person,
            personName = personName,
            icon = icon,
            text = text,
            clip = dialogueVoiceClip,
            emotion = "Idle"
        };
    }

    private IEnumerator PlayIntroCutscene()
    {
        if (frog == null || dialogueManager == null)
        {
            Debug.LogError("Tower Defence: для катсцены не найден игрок или DialogueManager.");
            yield break;
        }

        // PlayerInput is initialized by InputManager.Awake. Recover the
        // reference explicitly in case the scene was entered from another mode.
        if (InputManager.PlayerInput == null)
        {
            InputManager inputManager = FindObjectOfType<InputManager>();
            if (inputManager != null)
                InputManager.PlayerInput = inputManager.GetComponent<PlayerInput>();
        }

        float inputWait = 0f;
        while (InputManager.PlayerInput == null && inputWait < 5f)
        {
            inputWait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (InputManager.PlayerInput == null)
        {
            Debug.LogError("Tower Defence: не найден PlayerInput. Катсцена отменена.");
            yield break;
        }

        InputManager.PlayerInput.SwitchCurrentActionMap("Cutscene");

        Rigidbody2D body = frog.GetComponent<Rigidbody2D>();
        bool restoreSimulation = body != null && body.simulated;
        PlayerController frogController = frog.GetComponent<PlayerController>();
        bool restoreController = frogController != null && frogController.enabled;
        if (frogController != null)
            frogController.enabled = false;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        Vector3 target = new Vector3(
            grandpaPosition.x + 2f,
            grandpaPosition.y - GetPixelWorldSize(80f),
            frog.position.z);
        frog.position = new Vector3(frogStartX, target.y, frog.position.z);

        if (body != null)
            body.position = frog.position;

        SpriteRenderer frogRenderer = frog.GetComponent<SpriteRenderer>();
        if (frogRenderer != null)
            frogRenderer.flipX = false;
        Animator frogAnimator = frog.GetComponent<Animator>();
        if (frogAnimator == null)
            frogAnimator = frog.GetComponentInChildren<Animator>(true);
        SetFrogWalkAnimation(frogAnimator, -1f);

        while (Vector3.Distance(frog.position, target) > 0.025f)
        {
            frog.position = Vector3.MoveTowards(
                frog.position,
                target,
                frogWalkSpeed * Time.deltaTime);
            yield return null;
        }

        frog.position = target;
        if (frogAnimator != null)
            frogAnimator.SetFloat("Speed", 0f);
        if (body != null)
        {
            body.position = target;
            body.linearVelocity = Vector2.zero;
            body.simulated = restoreSimulation;
        }

        yield return new WaitForSeconds(0.15f);
        if (dialogueManager.dialogues == null ||
            dialogueManager.dialogues.Count == 0 ||
            dialogueManager.dialogues[0] == null ||
            dialogueManager.dialogues[0].dialogueLines == null ||
            dialogueManager.dialogues[0].dialogueLines.Count == 0)
        {
            Debug.LogError("Tower Defence: список реплик пуст, диалог не запущен.");
            yield break;
        }

        dialogueManager.dialogues[0].isPassed = false;
        if (!dialogueManager.TryStartDialogue(0))
        {
            Debug.LogError("Tower Defence: вступительный диалог не удалось открыть. Этап не будет запускаться.");
            yield break;
        }

        yield return new WaitUntil(
            () => dialogueManager == null ||
                dialogueManager.dialogues == null ||
                dialogueManager.dialogues.Count == 0 ||
                dialogueManager.dialogues[0] == null ||
                dialogueManager.dialogues[0].isPassed);

        if (dialogueManager == null ||
            dialogueManager.dialogues == null ||
            dialogueManager.dialogues.Count == 0 ||
            dialogueManager.dialogues[0] == null ||
            !dialogueManager.dialogues[0].isPassed)
        {
            Debug.LogError("Tower Defence: вступительный диалог не завершился. Запуск волн отменён.");
            yield break;
        }

        if (restoreController && frogController != null)
            frogController.enabled = true;
        if (InputManager.PlayerInput != null)
            InputManager.PlayerInput.SwitchCurrentActionMap("Player");

        if (gameManager != null)
            gameManager.BeginAfterIntro();
    }

    private void SetFrogWalkAnimation(Animator animator, float directionX)
    {
        if (animator == null)
            return;

        animator.SetFloat("MoveX", directionX);
        animator.SetFloat("MoveY", 0f);
        animator.SetFloat("Speed", 1f);
    }

    private void HandleLineDisplayed(DialogueLine line)
    {
        if (line != null && line.personName == "Лягушка" && line.text == "?!")
            StartCoroutine(ShakeCamera(0.75f, 0.18f));
    }

    private void Update()
    {
        if (centralDoorObject == null || frog == null)
            return;

        if (!doorOpened && InputManager.instance != null &&
            InputManager.instance.InteractionInput &&
            Vector2.Distance(frog.position, centralDoorObject.transform.position) <= doorInteractDistance)
        {
            if (!TryConsumeKeyFromInventory())
            {
                ShowKeyPickupMessage("Дверь заперта. Нужен ключ от дедушки.");
                return;
            }

            doorOpened = true;
            ApplyDoorTileSprites(openDoorTileSprites);
            if (centralDoorCollider != null)
                centralDoorCollider.enabled = false;

            ShowKeyPickupMessage("Дверь открыта — проходи внутрь!");
        }

        // Победа засчитывается только после того, как игрок действительно
        // проходит через освобождённый дверной проём.
        if (doorOpened && !doorTransitionStarted &&
            frog.position.y > centralDoorObject.transform.position.y + 0.65f &&
            Mathf.Abs(frog.position.x - centralDoorObject.transform.position.x) < 0.75f)
        {
            doorTransitionStarted = true;
            StartCoroutine(CompleteDoorTransition());
        }
    }

    private void HandleAllWavesDefeated()
    {
        if (postWaveSequenceStarted)
            return;

        postWaveSequenceStarted = true;
        StartCoroutine(PlayPostWaveSequence());
    }

    private IEnumerator PlayPostWaveSequence()
    {
        if (frog == null || dialogueManager == null || gameManager == null)
            yield break;

        if (InputManager.PlayerInput == null)
        {
            InputManager inputManager = FindObjectOfType<InputManager>();
            if (inputManager != null)
                InputManager.PlayerInput = inputManager.GetComponent<PlayerInput>();
        }

        if (InputManager.PlayerInput != null)
            InputManager.PlayerInput.SwitchCurrentActionMap("Cutscene");

        Rigidbody2D body = frog.GetComponent<Rigidbody2D>();
        bool restoreSimulation = body != null && body.simulated;
        PlayerController frogController = frog.GetComponent<PlayerController>();
        bool restoreController = frogController != null && frogController.enabled;
        if (frogController != null)
            frogController.enabled = false;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        // Лягушка делает короткий шаг вправо к дедушке перед разговором.
        Vector3 target = grandpaPosition + new Vector3(
            2f, -GetPixelWorldSize(40f), frog.position.z);
        Animator animator = frog.GetComponent<Animator>();
        if (animator == null)
            animator = frog.GetComponentInChildren<Animator>(true);
        SpriteRenderer frogRenderer = frog.GetComponent<SpriteRenderer>();
        if (frogRenderer != null)
            frogRenderer.flipX = false;
        SetFrogWalkAnimation(animator, -1f);

        while (Vector3.Distance(frog.position, target) > 0.025f)
        {
            frog.position = Vector3.MoveTowards(
                frog.position, target, frogWalkSpeed * Time.deltaTime);
            yield return null;
        }
        frog.position = target;
        if (animator != null)
            animator.SetFloat("Speed", 0f);
        if (body != null)
            body.position = target;

        yield return new WaitForSeconds(0.2f);

        GameObject frogObject = frog.gameObject;
        Sprite playerIcon = frogIcon;
        if (playerIcon == null && frogRenderer != null)
            playerIcon = frogRenderer.sprite;
        Sprite oldManIcon = grandpaSpriteAsset != null ? grandpaSpriteAsset : playerIcon;

        List<DialogueLine> lines = new List<DialogueLine>
        {
            MakeLine(grandpaObject, "Дед", oldManIcon, "Оххх... Спасибо маленький! Как звать то тебя?"),
            MakeLine(frogObject, "Лягушка", playerIcon, "Ква-ква"),
            MakeLine(grandpaObject, "Дед", oldManIcon, "Какое интересное имя, не то что в мое время. Раньше все было проще: и трава зеленее и я моложе. Ну ничего на вас вся надежда. Вот слушай, однажды я..."),
            MakeLine(frogObject, "Лягушка", playerIcon, "КВА"),
            MakeLine(grandpaObject, "Дед", oldManIcon, "Ой, что-то я заговорился совсем, держи ключ, за той дверью находятся нужные тебе книги, только остерегайся большого цербера! Он такой непоседа...")
        };

        dialogueManager.dialogues.Clear();
        dialogueManager.dialogues.Add(new Dialogue { dialogueLines = lines });
        dialogueManager.dialogues[0].isPassed = false;
        if (!dialogueManager.TryStartDialogue(0))
        {
            Debug.LogError("Tower Defence: финальный диалог не удалось запустить.");
            if (restoreSimulation && body != null) body.simulated = true;
            if (restoreController && frogController != null) frogController.enabled = true;
            if (InputManager.PlayerInput != null)
                InputManager.PlayerInput.SwitchCurrentActionMap("Player");
            yield break;
        }

        yield return new WaitUntil(() => dialogueManager == null ||
            dialogueManager.dialogues == null || dialogueManager.dialogues.Count == 0 ||
            dialogueManager.dialogues[0] == null || dialogueManager.dialogues[0].isPassed);

        if (dialogueManager == null || dialogueManager.dialogues.Count == 0 ||
            dialogueManager.dialogues[0] == null || !dialogueManager.dialogues[0].isPassed)
            yield break;

        yield return ThrowKeyToFloor();

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = restoreSimulation;
        }
        if (restoreController && frogController != null)
            frogController.enabled = true;
        if (InputManager.PlayerInput != null)
            InputManager.PlayerInput.SwitchCurrentActionMap("Player");

        if (gameManager != null)
            gameManager.ShowStatusMessage("Подбери ключ и открой дверь в центре стены");
    }

    private IEnumerator ThrowKeyToFloor()
    {
        if (keyPickupSprite == null || keyInventoryItem == null || grandpaObject == null)
        {
            Debug.LogWarning("Tower Defence: назначь keyPickupSprite и keyInventoryItem в инспекторе.");
            yield break;
        }

        GameObject keyObject = new GameObject("Tower Defence Key");
        keyObject.transform.position = grandpaObject.transform.position + Vector3.up * 0.7f;

        SpriteRenderer renderer = keyObject.AddComponent<SpriteRenderer>();
        renderer.sprite = keyPickupSprite;
        Vector2 keySpriteSize = keyPickupSprite.bounds.size;
        float keyWorldSize = GetPixelWorldSize(50f);
        keyObject.transform.localScale = new Vector3(
            keyWorldSize / Mathf.Max(0.001f, keySpriteSize.x),
            keyWorldSize / Mathf.Max(0.001f, keySpriteSize.y),
            1f);
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 40;

        Vector3 destination = grandpaObject.transform.position + new Vector3(2.2f, 0.05f, 0f);
        Vector3 start = keyObject.transform.position;
        float elapsed = 0f;
        const float throwDuration = 0.55f;
        while (elapsed < throwDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwDuration);
            Vector3 position = Vector3.Lerp(start, destination, t);
            position.y += Mathf.Sin(t * Mathf.PI) * 0.55f;
            keyObject.transform.position = position;
            yield return null;
        }
        keyObject.transform.position = destination;

        keyObject.AddComponent<CircleCollider2D>();
        keyObject.AddComponent<Rigidbody2D>();
        keyObject.AddComponent<TowerDefenceKeyPickup>()
            .Initialize(keyInventoryItem, this);
    }

    public void OnTowerDefenceKeyCollected()
    {
        if (gameManager != null)
            gameManager.NotifyKeyCollected();
    }

    public void ShowKeyPickupMessage(string message)
    {
        if (gameManager != null)
            gameManager.ShowStatusMessage(message);
    }

    private bool TryConsumeKeyFromInventory()
    {
        Inventory inventory = Inventory.EnsureInstance();
        if (inventory == null || keyInventoryItem == null || inventory.slots == null)
            return false;

        for (int i = 0; i < inventory.slots.Count; i++)
        {
            ItemStack stack = inventory.slots[i];
            if (stack != null && !stack.IsEmpty && stack.item == keyInventoryItem)
            {
                return RemoveOneKeyFromInventory(inventory, i);
            }
        }
        return false;
    }

    private static bool RemoveOneKeyFromInventory(Inventory inventory, int slotIndex)
    {
        if (inventory == null)
            return false;

        MethodInfo removeWithAmount = inventory.GetType().GetMethod(
            "RemoveAt", new[] { typeof(int), typeof(int) });
        if (removeWithAmount != null)
        {
            removeWithAmount.Invoke(inventory, new object[] { slotIndex, 1 });
            return true;
        }

        MethodInfo removeSingle = inventory.GetType().GetMethod(
            "RemoveAt", new[] { typeof(int) });
        if (removeSingle != null)
        {
            removeSingle.Invoke(inventory, new object[] { slotIndex });
            return true;
        }

        Debug.LogError("Tower Defence: Inventory.RemoveAt не найден.");
        return false;
    }

    private IEnumerator CompleteDoorTransition()
    {
        FloorFadeTransition fade = GetComponent<FloorFadeTransition>();
        if (fade != null)
            yield return fade.FadeToBlack(0.9f);

        if (gameManager != null)
            gameManager.CompleteAfterDoor();
    }

    private IEnumerator ShakeCamera(float duration, float magnitude)
    {
        if (sceneCamera == null)
            yield break;

        Vector3 startPosition = sceneCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float strength = 1f - elapsed / duration;
            Vector2 offset = Random.insideUnitCircle * magnitude * strength;
            sceneCamera.transform.localPosition =
                startPosition + new Vector3(offset.x, offset.y, 0f);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        sceneCamera.transform.localPosition = startPosition;
    }
}
