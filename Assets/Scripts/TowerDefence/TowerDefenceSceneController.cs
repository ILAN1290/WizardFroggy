using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class TowerDefenceSceneController : MonoBehaviour
{
    [Header("Персонажи")]
    [SerializeField] private Sprite grandpaSpriteAsset;
    [SerializeField] private Sprite roadTileSprite;
    [SerializeField] private Sprite frogIcon;

    [Header("Положение персонажей")]
    [SerializeField] private Vector3 grandpaPosition = new Vector3(6.45f, -3.2f, 0f);
    [SerializeField] private float frogStartX = 10.5f;
    [SerializeField] private float frogStopX = 5.85f;
    [SerializeField] private float frogWalkSpeed = 3.8f;

    private DialogueManager dialogueManager;
    private Transform frog;
    private Camera sceneCamera;
    private GameObject grandpaObject;
    private Texture2D solidTexture;
    private Sprite solidSprite;
    private Tile runtimeRoadTile;

    private void Awake()
    {
        dialogueManager = FindObjectOfType<DialogueManager>();

        GameObject frogObject = GameObject.Find("Frog");
        if (frogObject != null)
            frog = frogObject.transform;

        sceneCamera = Camera.main;
    }

    private IEnumerator Start()
    {
        // Дожидаемся завершения Awake у остальных объектов сцены.
        yield return null;

        if (dialogueManager == null)
            dialogueManager = FindObjectOfType<DialogueManager>();
        if (frog == null)
        {
            GameObject frogObject = GameObject.Find("Frog");
            if (frogObject != null)
                frog = frogObject.transform;
        }
        if (sceneCamera == null)
            sceneCamera = Camera.main;

        CreateRoad();
        CreateCenterDoor();
        CreateGrandpa();
        ConfigureIntroDialogue();

        if (dialogueManager == null || frog == null)
        {
            Debug.LogError("Tower Defence: катсцена не запущена — не найдены Frog или DialogueManager.");
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
        if (solidSprite != null)
            Destroy(solidSprite);
        if (solidTexture != null)
            Destroy(solidTexture);
    }

    private void CreateRoad()
    {
        // Дорожка строится клетками Tilemap из контрастного красно-коричневого тайла.
        if (roadTileSprite == null)
        {
            Debug.LogError("Tower Defence: не назначен тайл дорожки Tileset_48x48_9_15.");
            return;
        }

        Transform previousPath = transform.Find("Tower Defence Paths");
        if (previousPath != null)
            Destroy(previousPath.gameObject);

        GameObject pathRoot = new GameObject("Tower Defence Paths");
        pathRoot.transform.SetParent(transform, false);

        Grid grid = pathRoot.AddComponent<Grid>();
        grid.cellSize = Vector3.one;
        grid.cellGap = Vector3.zero;

        Tilemap tilemap = pathRoot.AddComponent<Tilemap>();
        TilemapRenderer renderer = pathRoot.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = -6; // Поверх пола (-10), под персонажами (0).

        runtimeRoadTile = ScriptableObject.CreateInstance<Tile>();
        runtimeRoadTile.name = "Runtime Tower Defence Road Tile";
        runtimeRoadTile.sprite = roadTileSprite;
        runtimeRoadTile.colliderType = Tile.ColliderType.None;

        HashSet<Vector3Int> roadCells = new HashSet<Vector3Int>();

        // Основной участок подходит к развилке с левой стороны.
        int forkY = Random.Range(-6, -4);
        AddRandomLaneSection(
            roadCells,
            new Vector2Int(-9, -5),
            new Vector2Int(0, forkY),
            -6,
            -4,
            0.45f);

        // Верхняя и нижняя ветки каждый запуск получают свою случайную форму.
        int upperY = Random.Range(-4, -2);
        int lowerY = Random.Range(-7, -5);

        AddRandomLaneSection(
            roadCells,
            new Vector2Int(0, forkY),
            new Vector2Int(3, upperY),
            -4,
            -2,
            0.35f);
        AddRandomLaneSection(
            roadCells,
            new Vector2Int(3, upperY),
            new Vector2Int(8, -5),
            -4,
            -2,
            0.35f);

        AddRandomLaneSection(
            roadCells,
            new Vector2Int(0, forkY),
            new Vector2Int(3, lowerY),
            -7,
            -5,
            0.35f);
        AddRandomLaneSection(
            roadCells,
            new Vector2Int(3, lowerY),
            new Vector2Int(8, -5),
            -7,
            -5,
            0.35f);

        foreach (Vector3Int cell in roadCells)
        {
            tilemap.SetTile(cell, runtimeRoadTile);

            // Небольшая разница оттенков сохраняет фактуру и убирает визуальную монотонность.
            float shade = Random.Range(0.92f, 1.04f);
            tilemap.SetColor(cell, new Color(shade, shade, shade, 1f));
        }
    }

    private void AddRandomLaneSection(
        HashSet<Vector3Int> cells,
        Vector2Int start,
        Vector2Int end,
        int minY,
        int maxY,
        float variationChance)
    {
        int previousY = start.y;
        int length = Mathf.Max(1, end.x - start.x);

        for (int x = start.x; x <= end.x; x++)
        {
            float t = (x - start.x) / (float)length;
            int y = Mathf.RoundToInt(Mathf.Lerp(start.y, end.y, t));

            if (x > start.x && x < end.x && Random.value < variationChance)
                y += Random.Range(-1, 2);

            if (x == start.x)
                y = start.y;
            else if (x == end.x)
                y = end.y;
            else
                y = Mathf.Clamp(y, minY, maxY);

            // Заполняем промежуточные ряды, чтобы повороты не оставляли дырок.
            int lowerY = Mathf.Min(previousY, y);
            int upperY = Mathf.Max(previousY, y);

            for (int row = lowerY; row <= upperY + 1; row++)
                cells.Add(new Vector3Int(x, row, 0));

            previousY = y;
        }
    }

    private void CreateCenterDoor()
    {
        if (transform.Find("Central Door") != null)
            return;

        CreateSolidSprite();

        GameObject doorRoot = new GameObject("Central Door");
        doorRoot.transform.SetParent(transform, false);
        doorRoot.transform.localPosition = new Vector3(0f, 0.6f, 0.25f);

        Color frame = new Color(0.18f, 0.085f, 0.045f, 1f);
        Color wood = new Color(0.43f, 0.22f, 0.115f, 1f);
        Color plank = new Color(0.57f, 0.31f, 0.16f, 1f);
        Color inset = new Color(0.31f, 0.15f, 0.075f, 1f);
        Color metal = new Color(0.82f, 0.63f, 0.24f, 1f);

        CreateDoorPart(doorRoot.transform, "Left Frame", new Vector3(-0.92f, 1.15f, 0f), new Vector2(0.24f, 2.45f), frame, -2);
        CreateDoorPart(doorRoot.transform, "Right Frame", new Vector3(0.92f, 1.15f, 0f), new Vector2(0.24f, 2.45f), frame, -2);
        CreateDoorPart(doorRoot.transform, "Top Frame", new Vector3(0f, 2.36f, 0f), new Vector2(2.08f, 0.28f), frame, -2);
        CreateDoorPart(doorRoot.transform, "Door Leaf", new Vector3(0f, 1.13f, 0.02f), new Vector2(1.58f, 2.18f), wood, -1);
        CreateDoorPart(doorRoot.transform, "Left Door Panel", new Vector3(-0.37f, 1.22f, 0f), new Vector2(0.50f, 1.58f), inset, 0);
        CreateDoorPart(doorRoot.transform, "Right Door Panel", new Vector3(0.37f, 1.22f, 0f), new Vector2(0.50f, 1.58f), inset, 0);
        CreateDoorPart(doorRoot.transform, "Left Plank", new Vector3(-0.37f, 1.22f, -0.01f), new Vector2(0.40f, 1.43f), plank, 1);
        CreateDoorPart(doorRoot.transform, "Right Plank", new Vector3(0.37f, 1.22f, -0.01f), new Vector2(0.40f, 1.43f), plank, 1);
        CreateDoorPart(doorRoot.transform, "Door Crossbar", new Vector3(0f, 0.88f, -0.02f), new Vector2(1.35f, 0.14f), wood, 2);
        CreateDoorPart(doorRoot.transform, "Door Handle", new Vector3(0.53f, 1.13f, -0.03f), new Vector2(0.12f, 0.12f), metal, 3);
    }

    private void CreateSolidSprite()
    {
        if (solidSprite != null)
            return;

        solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        solidTexture.name = "Tower Defence Solid Pixel";
        solidTexture.filterMode = FilterMode.Point;
        solidTexture.SetPixel(0, 0, Color.white);
        solidTexture.Apply();

        solidSprite = Sprite.Create(
            solidTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
    }

    private void CreateDoorPart(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Vector2 size,
        Color color,
        int sortingOrder)
    {
        GameObject part = new GameObject(objectName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = solidSprite;
        renderer.color = color;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;
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

        // У спрайта дедушки размер около 24x23 единиц при PPU=100.
        // Уменьшаем его до размера персонажа арены Boss и ставим у правого барьера.
        grandpaObject.transform.localScale = Vector3.one * 0.085f;

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

        yield return new WaitUntil(() => InputManager.PlayerInput != null);
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
        dialogueManager.StartDialogue(0);

        yield return new WaitUntil(
            () => dialogueManager.dialogues.Count > 0 && dialogueManager.dialogues[0].isPassed);

        if (InputManager.PlayerInput != null)
            InputManager.PlayerInput.SwitchCurrentActionMap("Player");
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
