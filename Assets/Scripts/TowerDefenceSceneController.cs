using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TowerDefenceSceneController : MonoBehaviour
{
    [Header("Персонажи")]
    [SerializeField] private Texture2D grandpaSheet;
    [SerializeField] private Sprite frogIcon;

    [Header("Положение персонажей")]
    [SerializeField] private Vector3 grandpaPosition = new Vector3(7.25f, -2.2f, 0f);
    [SerializeField] private float frogStartX = 10.5f;
    [SerializeField] private float frogStopX = 5.85f;
    [SerializeField] private float frogWalkSpeed = 3.8f;

    private DialogueManager dialogueManager;
    private Transform frog;
    private Camera sceneCamera;
    private GameObject grandpaObject;
    private Sprite grandpaSprite;
    private Texture2D solidTexture;
    private Sprite solidSprite;
    private Material pathMaterial;

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
        CreateRoad();
        CreateCenterDoor();
        CreateGrandpa();
        ConfigureIntroDialogue();

        if (dialogueManager != null)
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

        if (pathMaterial != null)
            Destroy(pathMaterial);
        if (solidSprite != null)
            Destroy(solidSprite);
        if (solidTexture != null)
            Destroy(solidTexture);
        if (grandpaSprite != null)
            Destroy(grandpaSprite);
    }

    private void CreateRoad()
    {
        if (transform.Find("Tower Defence Paths") != null)
            return;

        GameObject pathRoot = new GameObject("Tower Defence Paths");
        pathRoot.transform.SetParent(transform, false);

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("Tower Defence: не найден шейдер Sprites/Default.");
            return;
        }

        pathMaterial = new Material(shader);
        pathMaterial.name = "Tower Defence Path Material";

        Color outline = new Color(0.20f, 0.12f, 0.075f, 1f);
        Color ground = new Color(0.60f, 0.42f, 0.27f, 1f);

        Vector3[] approach =
        {
            new Vector3(-8.2f, -4.35f, 0.15f),
            new Vector3(-4.3f, -4.35f, 0.15f),
            new Vector3(-1.5f, -3.65f, 0.15f),
            new Vector3(0.35f, -3.65f, 0.15f)
        };

        Vector3[] upperBranch =
        {
            new Vector3(0.35f, -3.65f, 0.15f),
            new Vector3(2.2f, -2.25f, 0.15f),
            new Vector3(5.0f, -2.25f, 0.15f),
            new Vector3(7.9f, -3.55f, 0.15f)
        };

        Vector3[] lowerBranch =
        {
            new Vector3(0.35f, -3.65f, 0.15f),
            new Vector3(2.2f, -5.25f, 0.15f),
            new Vector3(5.0f, -5.25f, 0.15f),
            new Vector3(7.9f, -3.55f, 0.15f)
        };

        CreateRoadSegment(pathRoot.transform, "Main Path Border", approach, 0.92f, outline, -7);
        CreateRoadSegment(pathRoot.transform, "Main Path Surface", approach, 0.66f, ground, -6);
        CreateRoadSegment(pathRoot.transform, "Upper Path Border", upperBranch, 0.92f, outline, -7);
        CreateRoadSegment(pathRoot.transform, "Upper Path Surface", upperBranch, 0.66f, ground, -6);
        CreateRoadSegment(pathRoot.transform, "Lower Path Border", lowerBranch, 0.92f, outline, -7);
        CreateRoadSegment(pathRoot.transform, "Lower Path Surface", lowerBranch, 0.66f, ground, -6);
    }

    private void CreateRoadSegment(
        Transform parent,
        string objectName,
        Vector3[] points,
        float width,
        Color color,
        int sortingOrder)
    {
        GameObject segment = new GameObject(objectName);
        segment.transform.SetParent(parent, false);

        LineRenderer line = segment.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.sharedMaterial = pathMaterial;
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerName = "Default";
        line.sortingOrder = sortingOrder;
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

        if (grandpaSheet == null || grandpaSheet.width < 48 || grandpaSheet.height < 48)
        {
            Debug.LogError("Tower Defence: не назначен спрайт-лист дедушки.");
            return;
        }

        // Первый персонаж в Characters_MV — седовласый старик.
        Rect spriteRect = new Rect(0f, grandpaSheet.height - 48f, 48f, 48f);
        grandpaSprite = Sprite.Create(
            grandpaSheet,
            spriteRect,
            new Vector2(0.5f, 0.12f),
            48f);
        grandpaSprite.name = "Grandpa Runtime Sprite";

        grandpaObject = new GameObject("Grandpa");
        grandpaObject.transform.SetParent(transform, false);
        grandpaObject.transform.position = grandpaPosition;
        grandpaObject.transform.localScale = Vector3.one * 1.65f;

        SpriteRenderer renderer = grandpaObject.AddComponent<SpriteRenderer>();
        renderer.sprite = grandpaSprite;
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

        Sprite oldManIcon = grandpaSprite != null ? grandpaSprite : playerIcon;

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

        if (InputManager.PlayerInput != null)
            InputManager.PlayerInput.SwitchCurrentActionMap("Cutscene");

        Rigidbody2D body = frog.GetComponent<Rigidbody2D>();
        bool restoreSimulation = body != null && body.simulated;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        Vector3 target = new Vector3(frogStopX, grandpaPosition.y - 0.2f, frog.position.z);
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
