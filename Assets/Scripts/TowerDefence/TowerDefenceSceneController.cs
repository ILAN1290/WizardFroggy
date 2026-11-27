using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private Sprite closedDoorSprite;
    [SerializeField] private Sprite frogIcon;

    [Header("Положение персонажей")]
    [SerializeField] private Vector3 grandpaPosition = new Vector3(-6.5f, -4.5f, 0f);
    [SerializeField] private float frogStartX = 10.5f;
    [SerializeField] private float frogStopX = -6.8f;
    [SerializeField] private float frogWalkSpeed = 3.8f;

    [SerializeField] private DialogueManager dialogueManager;
    private Transform frog;
    private Camera sceneCamera;
    private GameObject grandpaObject;
    private Tile runtimeRoadTile;
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

        if (runtimeRoadTile != null)
            Destroy(runtimeRoadTile);
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
            new Vector3(-7.5f, -3.5f, 0f),
            new Vector3(-7.5f, -4.5f, 0f),
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

        if (closedDoorSprite == null)
        {
            Debug.LogError("Tower Defence: не назначен спрайт закрытой двери.");
            return;
        }

        GameObject door = new GameObject("Central Door");
        door.transform.SetParent(transform, false);
        // Дверь стоит в свободном промежутке между верхними прямыми дорожки.
        door.transform.localPosition = new Vector3(0f, -0.5f, 0.15f);

        SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
        renderer.sprite = closedDoorSprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 8;
        renderer.transform.localScale = Vector3.one * 0.5f;
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
                "Аааа, милок, конечно я тебе помогу! Только послушай, это место очень страшное! Тут обитают духи! Моя женушка говорила: «ОДЕВАЙ ШАПКУ МЯУРОН!», а я не одевал, да что ты понимаешь, какое поколение пошло, вы ни дать ни взять совсем бес....."),
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
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        Vector3 target = new Vector3(frogStopX, grandpaPosition.y + 0.15f, frog.position.z);
        frog.position = new Vector3(frogStartX, target.y, frog.position.z);

        if (body != null)
            body.position = frog.position;

        SpriteRenderer frogRenderer = frog.GetComponent<SpriteRenderer>();
        if (frogRenderer != null)
            frogRenderer.flipX = true;

        while (Vector3.Distance(frog.position, target) > 0.025f)
        {
            frog.position = Vector3.MoveTowards(
                frog.position,
                target,
                frogWalkSpeed * Time.deltaTime);
            yield return null;
        }

        frog.position = target;
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

        if (InputManager.PlayerInput != null)
            InputManager.PlayerInput.SwitchCurrentActionMap("Player");

        if (gameManager != null)
            gameManager.BeginAfterIntro();
    }

    private void HandleLineDisplayed(DialogueLine line)
    {
        if (line != null && line.personName == "Лягушка" && line.text == "?!")
            StartCoroutine(ShakeCamera(0.75f, 0.18f));
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
