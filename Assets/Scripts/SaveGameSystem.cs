using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;

public class SaveGameSystem : MonoBehaviour
{
    [Serializable]
    private class InventoryEntry
    {
        public int slot;
        public string itemId;
        public int count;
    }

    [Serializable]
    private class SaveSlotData
    {
        public bool exists;
        public int resumeStage;
        public int progressPercent;
        public bool roguelikeCompleted;
        public bool towerDefenceCompleted;
        public bool bossCompleted;
        public string savedAt;
        public int mushrooms;
        public int highestUnlockedFloor;
        public int staffDamageLevel;
        public int manaConsumptionLevel;
        public int manaRegenerationLevel;
        public int maximumHealthLevel;
        public int defenseLevel;
        public float health;
        public float mana;
        public List<InventoryEntry> inventory = new List<InventoryEntry>();
    }

    private enum ScreenMode
    {
        None,
        Load,
        SaveAfterRoguelike,
        SaveAfterTowerDefence
    }

    private const string ActiveSlotKey = "WizardFroggy.ActiveSaveSlot";
    private const int SlotCount = 3;
    private const int InventorySize = 20;

    [Header("Item references used to restore saved inventory")]
    [SerializeField] private Item[] itemCatalog = new Item[0];

    [Header("UI Game / Merchenant assets")]
    [SerializeField] private Texture2D buttonTexture;
    [SerializeField] private Texture2D deleteTexture;

    [Header("Save/load menu sound")]
    [SerializeField] private AudioClip menuOpenSound;
    [SerializeField] private AudioSource uiAudioSource;

    [Header("Main menu button textures (empty = keep scene sprite)")]
    [SerializeField] private Sprite startMenuButtonTexture;
    [SerializeField] private Sprite loadButtonSprite;
    [SerializeField] private Sprite optionsMenuButtonTexture;
    [SerializeField] private Sprite exitMenuButtonTexture;

    [Header("Main menu button positions")]
    [SerializeField] private Vector2 startButtonPosition = new Vector2(414.8f, -38f);
    [SerializeField] private Vector2 loadButtonPosition = new Vector2(414.8f, -155f);
    [SerializeField] private Vector2 optionsButtonPosition = new Vector2(289.8f, -272f);
    [SerializeField] private Vector2 exitButtonPosition = new Vector2(539.8f, -272f);

    [Header("UI font")]
    [SerializeField] private TMP_FontAsset menuFontAsset;

    [Header("Three save slot textures (one texture per slot)")]

    public static SaveGameSystem Instance { get; private set; }

    private Canvas overlayCanvas;
    private EventSystem persistentEventSystem;
    private ScreenMode screenMode;
    private int activeSlot = -1;
    private string nextScene = "";
    private float previousTimeScale = 1f;
    private bool transitionPaused;
    private List<InventoryEntry> pendingInventoryRestore;
    private Sprite buttonSprite;
    private Sprite deleteSprite;
    private Sprite loadActionIcon;
    private Sprite saveActionIcon;
    private Sprite deleteActionIcon;
    private Button[] currentSlotButtons = new Button[SlotCount];
    private int selectedSaveSlotIndex;

    public TMP_FontAsset MenuFontAsset => menuFontAsset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject systemObject = new GameObject("SaveGameSystem");
        systemObject.AddComponent<SaveGameSystem>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Instance.AbsorbConfiguration(this);
            Destroy(this);
            return;
        }

        Instance = this;
        activeSlot = PlayerPrefs.GetInt(ActiveSlotKey, -1);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void AbsorbConfiguration(SaveGameSystem source)
    {
        if (source == null) return;
        if (source.itemCatalog != null && source.itemCatalog.Length > 0)
            itemCatalog = source.itemCatalog;
        if (source.buttonTexture != null) buttonTexture = source.buttonTexture;
        if (source.deleteTexture != null) deleteTexture = source.deleteTexture;
        if (source.menuOpenSound != null) menuOpenSound = source.menuOpenSound;
        if (source.uiAudioSource != null) uiAudioSource = source.uiAudioSource;
        if (source.startMenuButtonTexture != null) startMenuButtonTexture = source.startMenuButtonTexture;
        if (source.loadButtonSprite != null) loadButtonSprite = source.loadButtonSprite;
        if (source.optionsMenuButtonTexture != null) optionsMenuButtonTexture = source.optionsMenuButtonTexture;
        if (source.exitMenuButtonTexture != null) exitMenuButtonTexture = source.exitMenuButtonTexture;
        startButtonPosition = source.startButtonPosition;
        loadButtonPosition = source.loadButtonPosition;
        optionsButtonPosition = source.optionsButtonPosition;
        exitButtonPosition = source.exitButtonPosition;
        if (source.menuFontAsset != null) menuFontAsset = source.menuFontAsset;
        RefreshSprites();
    }

    private void Start()
    {
        RefreshSprites();
        EnsurePersistentEventSystem();
        ApplyMenuFontToLoadedUI();
        if (uiAudioSource == null)
            uiAudioSource = gameObject.AddComponent<AudioSource>();
        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
        uiAudioSource.ignoreListenerPause = true;
    }

    private void Update()
    {
        if (overlayCanvas == null ||
            (screenMode != ScreenMode.Load &&
             screenMode != ScreenMode.SaveAfterRoguelike &&
             screenMode != ScreenMode.SaveAfterTowerDefence))
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (screenMode == ScreenMode.Load && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseScreen();
            return;
        }

        if (keyboard.upArrowKey.wasPressedThisFrame)
        {
            MoveSelectedSaveSlot(-1);
        }
        else if (keyboard.downArrowKey.wasPressedThisFrame)
        {
            MoveSelectedSaveSlot(1);
        }

        if (keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            SubmitSelectedSaveSlot();
        }
    }

    private void MoveSelectedSaveSlot(int direction)
    {
        selectedSaveSlotIndex = (selectedSaveSlotIndex + direction + SlotCount) % SlotCount;
        SelectSaveSlot(selectedSaveSlotIndex);
    }

    private void SelectSaveSlot(int index)
    {
        selectedSaveSlotIndex = Mathf.Clamp(index, 0, SlotCount - 1);
        if (currentSlotButtons == null || currentSlotButtons.Length != SlotCount)
            return;

        Button button = currentSlotButtons[selectedSaveSlotIndex];
        if (button != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private void SubmitSelectedSaveSlot()
    {
        SaveSlotData data = ReadSlot(selectedSaveSlotIndex);
        if (screenMode == ScreenMode.Load)
        {
            if (data.exists)
                LoadSlot(selectedSaveSlotIndex);
            return;
        }

        SaveTransitionToSlot(selectedSaveSlotIndex);
    }

    private IEnumerator ApplyMenuFontAfterSceneLoad()
    {
        yield return null;
        ApplyMenuFontToLoadedUI();
    }

    private void ApplyMenuFontToLoadedUI()
    {
        if (menuFontAsset == null) return;

        TMP_Text[] labels = FindObjectsOfType<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null)
                labels[i].font = menuFontAsset;
        }
    }

    private void PlayMenuOpenSound()
    {
        if (menuOpenSound == null) return;
        if (uiAudioSource == null)
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
            uiAudioSource.playOnAwake = false;
            uiAudioSource.spatialBlend = 0f;
            uiAudioSource.ignoreListenerPause = true;
        }
        uiAudioSource.PlayOneShot(menuOpenSound);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void RefreshSprites()
    {
        buttonSprite = MakeSprite(buttonTexture, buttonSprite);
        deleteSprite = MakeSprite(deleteTexture, deleteSprite);

        if (loadActionIcon == null) loadActionIcon = CreatePixelActionIcon(0);
        if (saveActionIcon == null) saveActionIcon = CreatePixelActionIcon(1);
        if (deleteActionIcon == null) deleteActionIcon = CreatePixelActionIcon(2);
    }

    // 0 = download arrow, 1 = save arrow, 2 = trash can.
    private static Sprite CreatePixelActionIcon(int iconType)
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = iconType == 0 ? "Save UI • Download" :
            iconType == 1 ? "Save UI • Save" : "Save UI • Delete";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 ink = new Color32(255, 239, 207, 255);
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        if (iconType == 0 || iconType == 1)
        {
            bool down = iconType == 0;
            // Shared shaft, connected to the flat side of the arrow head.
            for (int y = 18; y < 29; y++)
                for (int x = 13; x <= 18; x++)
                    pixels[y * size + x] = ink;

            for (int row = 0; row < 14; row++)
            {
                int y = down ? 18 - row : 5 + row;
                int halfWidth = down ? 15 - row : 2 + row;
                for (int x = 16 - halfWidth; x <= 16 + halfWidth; x++)
                    pixels[y * size + x] = ink;
            }
        }
        else
        {
            // Lid and handle.
            for (int x = 6; x <= 25; x++) pixels[25 * size + x] = ink;
            for (int x = 11; x <= 20; x++) pixels[27 * size + x] = ink;
            for (int y = 27; y <= 29; y++)
                for (int x = 13; x <= 18; x++)
                    pixels[y * size + x] = ink;

            // Slightly tapered bin body.
            for (int y = 7; y <= 23; y++)
            {
                int inset = (23 - y) / 7;
                for (int x = 9 + inset; x <= 22 - inset; x++)
                    pixels[y * size + x] = ink;
            }

            // Dark vertical slots in the bin.
            Color32 cut = new Color32(70, 48, 39, 255);
            for (int y = 10; y <= 19; y++)
            {
                pixels[y * size + 13] = cut;
                pixels[y * size + 18] = cut;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite MakeSprite(Texture2D texture, Sprite current)
    {
        if (texture == null) return current;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f);
    }

    private void EnsurePersistentEventSystem()
    {
        EventSystem[] systems = FindObjectsOfType<EventSystem>();
        if (persistentEventSystem == null)
        {
            if (systems.Length > 0)
            {
                persistentEventSystem = systems[0];
                DontDestroyOnLoad(persistentEventSystem.gameObject);
            }
            else
            {
                GameObject eventObject = new GameObject("Save UI EventSystem");
                persistentEventSystem = eventObject.AddComponent<EventSystem>();
                eventObject.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(eventObject);
            }
        }

        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null && systems[i] != persistentEventSystem)
                Destroy(systems[i].gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        EnsurePersistentEventSystem();
        StartCoroutine(ApplyMenuFontAfterSceneLoad());

        if (pendingInventoryRestore != null)
            StartCoroutine(RestoreInventoryAfterSceneLoad());

        if (scene.name == "Intro")
            StartCoroutine(PrepareMainMenu());

        // TowerDefence is currently an intentionally empty placeholder scene.
        // Its completion screen is shown here until the actual gameplay is added.
        if (scene.name == "TowerDefence")
            StartCoroutine(ShowTowerDefencePlaceholder());

        if (scene.name == "End")
            StartCoroutine(AutoSaveBossCompletion());
    }

    private IEnumerator PrepareMainMenu()
    {
        yield return null;
        yield return null;

        GameObject menuObject = GameObject.Find("MainMenuAndLogo");
        if (menuObject == null) yield break;

        Transform menu = menuObject.transform;
        Transform start = menu.Find("Start Button");
        Transform load = menu.Find("Load Game Button");
        Transform exit = menu.Find("Exit Button");
        if (load == null && exit != null)
        {
            GameObject loadObject = Instantiate(exit.gameObject, exit.parent);
            loadObject.name = "Load Game Button";
            load = loadObject.transform;
            Image loadButtonImage = loadObject.GetComponent<Image>();
            if (loadButtonImage != null)
            {
                if (loadButtonSprite != null)
                    loadButtonImage.sprite = loadButtonSprite;
                loadButtonImage.preserveAspect = loadButtonImage.sprite != null;
            }
            Button loadButtonComponent = loadObject.GetComponent<Button>();
            if (loadButtonComponent != null)
                loadButtonComponent.onClick.RemoveAllListeners();
            MainMenuLoadButton loadHandler = loadObject.GetComponent<MainMenuLoadButton>();
            if (loadHandler == null)
                loadObject.AddComponent<MainMenuLoadButton>();
        }
        Transform options = menu.Find("Options Button");

        ApplyMenuButtonTexture(start, startMenuButtonTexture);
        ApplyMenuButtonTexture(load, loadButtonSprite);
        ApplyMenuButtonTexture(options, optionsMenuButtonTexture);
        ApplyMenuButtonTexture(exit, exitMenuButtonTexture);

        ApplyMenuFontToLoadedUI();
        SetMenuRect(start, startButtonPosition, new Vector2(500f, 100f));
        SetMenuRect(load, loadButtonPosition, new Vector2(500f, 100f));
        SetMenuRect(options, optionsButtonPosition, new Vector2(250f, 88f));
        SetMenuRect(exit, exitButtonPosition, new Vector2(250f, 88f));

        GameObject[] buttons = new GameObject[4];
        buttons[0] = start != null ? start.gameObject : null;
        buttons[1] = load != null ? load.gameObject : null;
        buttons[2] = options != null ? options.gameObject : null;
        buttons[3] = exit != null ? exit.gameObject : null;

        ButtonSelectionManager selectionManager = menuObject.GetComponent<ButtonSelectionManager>();
        if (selectionManager != null)
            selectionManager.Buttons = buttons;

        if (load != null)
        {
            ButtonSelectionController controller = load.GetComponent<ButtonSelectionController>();
            if (controller == null)
                controller = load.gameObject.AddComponent<ButtonSelectionController>();
            if (selectionManager != null)
            {
                FieldInfo field = typeof(ButtonSelectionController).GetField(
                    "buttonSelectionManager", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) field.SetValue(controller, selectionManager);
            }
        }

        StartingGame startingGame = FindObjectOfType<StartingGame>();
        if (startingGame != null)
            SetPrivateField(startingGame, "buttons", buttons);

        ToggleOptions toggleOptions = FindObjectOfType<ToggleOptions>();
        if (toggleOptions != null)
            SetPrivateField(toggleOptions, "mainMenuButtons", buttons);

        ConfigureNavigation(start, load, options, exit);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null) return;
        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null) field.SetValue(target, value);
    }

    private static void ApplyMenuButtonTexture(Transform target, Sprite texture)
    {
        if (target == null || texture == null) return;
        Image image = target.GetComponent<Image>();
        if (image == null) return;
        image.sprite = texture;
        image.preserveAspect = true;
        image.color = Color.white;
    }

    private static void SetMenuRect(Transform target, Vector2 position, Vector2 size)
    {
        if (target == null) return;
        RectTransform rect = target as RectTransform;
        if (rect == null) return;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void ConfigureNavigation(Transform start, Transform load,
        Transform options, Transform exit)
    {
        Button startButton = start != null ? start.GetComponent<Button>() : null;
        Button loadButton = load != null ? load.GetComponent<Button>() : null;
        Button optionsButton = options != null ? options.GetComponent<Button>() : null;
        Button exitButton = exit != null ? exit.GetComponent<Button>() : null;

        SetNavigation(startButton, null, loadButton, null, null);
        SetNavigation(loadButton, startButton, optionsButton, null, null);
        SetNavigation(optionsButton, loadButton, exitButton, null, exitButton);
        SetNavigation(exitButton, loadButton, null, optionsButton, null);
    }

    private static void SetNavigation(Button button, Selectable up, Selectable down,
        Selectable left, Selectable right)
    {
        if (button == null) return;
        Navigation navigation = new Navigation();
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        navigation.selectOnLeft = left;
        navigation.selectOnRight = right;
        button.navigation = navigation;
    }

    public void BeginNewGame()
    {
        activeSlot = -1;
        PlayerPrefs.DeleteKey(ActiveSlotKey);
        PlayerRunData.EnsureInstance().ResetForNewRun();

        Inventory inventory = Inventory.EnsureInstance();
        inventory.Init(InventorySize);
        for (int i = 0; i < inventory.slots.Count; i++)
            inventory.slots[i].Clear();
        inventory.NotifyChanged();

        pendingInventoryRestore = new List<InventoryEntry>();
        PlayerPrefs.Save();
    }

    public void OpenLoadMenu()
    {
        PlayMenuOpenSound();
        transitionPaused = false;
        screenMode = ScreenMode.Load;
        BuildScreen("Загрузка игры", "Выберите один из трёх слотов. Нажмите на карточку сохранения, чтобы загрузить игру.");
    }

    public void ShowRoguelikeTransition()
    {
        PlayMenuOpenSound();
        nextScene = "TowerDefence";
        previousTimeScale = Time.timeScale;
        transitionPaused = true;
        Time.timeScale = 0f;
        screenMode = ScreenMode.SaveAfterRoguelike;
        BuildScreen("Рогалик пройден!", "Сохраните прохождение в один из трёх слотов или продолжите без сохранения.");
    }

    private IEnumerator ShowTowerDefencePlaceholder()
    {
        yield return null;
        yield return null;
        if (SceneManager.GetActiveScene().name != "TowerDefence") yield break;

        PlayMenuOpenSound();
        nextScene = "Boss";
        previousTimeScale = Time.timeScale;
        transitionPaused = true;
        Time.timeScale = 0f;
        screenMode = ScreenMode.SaveAfterTowerDefence;
        BuildScreen("Tower Defence пройден!", "Сцена TowerDefence пока пустая. Сохраните прогресс и переходите к финальному боссу.");
    }

    private void BuildScreen(string heading, string description)
    {
        CloseCanvasOnly();
        RefreshSprites();

        GameObject canvasObject = new GameObject("SaveGameOverlay",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 32000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        RectTransform backdrop = CreateRect("Black transition", canvasObject.transform,
            new Vector2(1920f, 1080f), Vector2.zero);
        StretchToParent(backdrop);
        Image black = backdrop.gameObject.AddComponent<Image>();
        black.color = new Color(0f, 0f, 0f, 0.97f);
        black.raycastTarget = true;

        RectTransform panel = CreateRect("Save panel", canvasObject.transform,
            new Vector2(1580f, 950f), Vector2.zero);
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.10f, 0.075f, 0.06f, 0.98f);
        Outline panelOutline = panel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.61f, 0.40f, 0.20f, 1f);
        panelOutline.effectDistance = new Vector2(4f, -4f);

        CreateText("Heading", panel, heading, new Vector2(1240f, 76f),
            new Vector2(0f, 405f), 52, TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateText("Description", panel, description, new Vector2(1320f, 58f),
            new Vector2(0f, 345f), 25, TextAnchor.MiddleCenter, FontStyle.Normal);

        currentSlotButtons = new Button[SlotCount];
        for (int i = 0; i < SlotCount; i++)
            BuildSlotCard(panel, i);

        if (screenMode == ScreenMode.Load)
        {
            SetMainMenuEnabled(false);
            CreateText("Load slot hint", panel, "Выберите одну из трёх полок с сохранениями",
                new Vector2(1300f, 36f), new Vector2(0f, -335f), 20,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateButton(panel, "", new Vector2(280f, 74f),
                new Vector2(0f, -410f), CloseScreen, deleteSprite);
        }
        else
        {
            CreateText("Save slot hint", panel, "НАЖМИТЕ НА ЛЮБУЮ ПОЛКУ, ЧТОБЫ СОХРАНИТЬ ИГРУ",
                new Vector2(1300f, 36f), new Vector2(0f, -335f), 20,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateButton(panel, "Продолжить", new Vector2(330f, 78f),
                new Vector2(0f, -410f), ContinueToNextStage, buttonSprite);
        }

        int initialSlot = screenMode == ScreenMode.Load
            ? FindFirstExistingSaveSlot()
            : (activeSlot >= 0 && activeSlot < SlotCount ? activeSlot : FindFirstEmptySaveSlot());
        SelectSaveSlot(initialSlot);
    }

    private void BuildSlotCard(RectTransform panel, int index)
    {
        SaveSlotData data = ReadSlot(index);
        int slotIndex = index;
        float y = 205f - index * 205f;

        RectTransform card = CreateRect("Save slot " + (index + 1), panel,
            new Vector2(1390f, 184f), new Vector2(0f, y));
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.16f, 0.115f, 0.08f, 0.96f);
        cardImage.raycastTarget = true;

        Button shelfButton = card.gameObject.AddComponent<Button>();
        shelfButton.targetGraphic = cardImage;
        currentSlotButtons[slotIndex] = shelfButton;
        ColorBlock shelfColors = shelfButton.colors;
        shelfColors.normalColor = cardImage.color;
        shelfColors.highlightedColor = new Color(0.25f, 0.18f, 0.12f, 1f);
        shelfColors.selectedColor = shelfColors.highlightedColor;
        shelfColors.pressedColor = new Color(0.10f, 0.07f, 0.05f, 1f);
        shelfColors.disabledColor = new Color(0.12f, 0.09f, 0.07f, 0.65f);
        shelfButton.colors = shelfColors;
        Navigation shelfNavigation = new Navigation();
        shelfNavigation.mode = Navigation.Mode.None;
        shelfButton.navigation = shelfNavigation;

        if (screenMode == ScreenMode.Load)
        {
            shelfButton.interactable = true;
            if (data.exists)
                shelfButton.onClick.AddListener(delegate { LoadSlot(slotIndex); });
        }
        else
        {
            shelfButton.onClick.AddListener(delegate { SaveTransitionToSlot(slotIndex); });
        }

        Outline cardOutline = card.gameObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.50f, 0.34f, 0.18f, 1f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        CreateText("Slot title", card, "СЛОТ " + (index + 1),
            new Vector2(310f, 30f), new Vector2(-470f, 73f), 22,
            TextAnchor.MiddleLeft, FontStyle.Bold);

        string completed = data.exists
            ? "Рогалик: " + PassStatus(data.roguelikeCompleted) +
              "\\nTower Defence: " + PassStatus(data.towerDefenceCompleted) +
              "\\nБосс: " + PassStatus(data.bossCompleted)
            : "Рогалик: не пройден\\nTower Defence: не пройден\\nБосс: не пройден";

        string inventory = data.exists
            ? "Грибочки: " + data.mushrooms +
              "\\n" + GetInventorySummary(data.inventory) +
              "\\nПрокачка: урон " + data.staffDamageLevel +
              ", мана " + data.manaConsumptionLevel + "/" + data.manaRegenerationLevel +
              ", здоровье " + data.maximumHealthLevel +
              ", защита " + data.defenseLevel
            : "Грибочки: 0\\nПредметов нет\\nПрокачка: не улучшена";

        string progressAndDate = data.exists
            ? data.progressPercent + "%\\nСохранено: " + data.savedAt
            : "0%\\nДата сохранения: —";

        CreateSaveInfoBox(card, "Completed box", "ПРОЙДЕНО",
            completed, new Vector2(330f, 126f), new Vector2(-490f, -8f), 15,
            TextAnchor.MiddleLeft);
        CreateSaveInfoBox(card, "Inventory box", "ИНВЕНТАРЬ",
            inventory, new Vector2(420f, 126f), new Vector2(-105f, -8f), 14,
            TextAnchor.MiddleLeft);
        CreateSaveInfoBox(card, "Progress box", "ПРОГРЕСС",
            progressAndDate, new Vector2(290f, 126f), new Vector2(265f, -8f), 19,
            TextAnchor.MiddleCenter);

        if (screenMode == ScreenMode.Load)
        {
            CreateIconButton(card, "Load save " + (slotIndex + 1),
                new Vector2(64f, 64f), new Vector2(525f, 0f),
                delegate { LoadSlot(slotIndex); }, loadActionIcon, data.exists);
        }
        else
        {
            CreateIconButton(card, "Save slot " + (slotIndex + 1),
                new Vector2(64f, 64f), new Vector2(525f, 0f),
                delegate { SaveTransitionToSlot(slotIndex); }, saveActionIcon, true);
        }

        CreateIconButton(card, "Delete save " + (slotIndex + 1),
            new Vector2(64f, 64f), new Vector2(615f, 0f),
            delegate { DeleteSlot(slotIndex); }, deleteActionIcon, data.exists);
    }

    private void CreateSaveInfoBox(Transform parent, string objectName,
        string heading, string body, Vector2 size, Vector2 position,
        int bodyFontSize, TextAnchor bodyAlignment)
    {
        RectTransform box = CreateRect(objectName, parent, size, position);
        Image background = box.gameObject.AddComponent<Image>();
        background.color = new Color(0.105f, 0.075f, 0.06f, 0.96f);
        background.raycastTarget = false;

        Outline outline = box.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.36f, 0.25f, 0.17f, 1f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        CreateText(objectName + " heading", box, heading,
            new Vector2(size.x - 18f, 25f), new Vector2(0f, 47f), 16,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateText(objectName + " body", box, body,
            new Vector2(size.x - 24f, 88f), new Vector2(0f, -12f), bodyFontSize,
            bodyAlignment, FontStyle.Normal);
    }

    private Button CreateIconButton(Transform parent, string objectName,
        Vector2 size, Vector2 position, UnityAction action, Sprite iconSprite,
        bool interactable)
    {
        RectTransform rect = CreateRect(objectName, parent, size, position);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.24f, 0.16f, 0.12f, 1f);
        background.raycastTarget = true;
        Outline buttonOutline = rect.gameObject.AddComponent<Outline>();
        buttonOutline.effectColor = new Color(0.62f, 0.43f, 0.26f, 1f);
        buttonOutline.effectDistance = new Vector2(2f, -2f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.interactable = interactable;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.88f, 0.68f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.72f, 0.59f, 0.43f, 1f);
        colors.disabledColor = new Color(0.42f, 0.38f, 0.34f, 0.48f);
        button.colors = colors;

        Navigation navigation = new Navigation();
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        RectTransform iconRect = CreateRect("Icon", rect,
            new Vector2(34f, 34f), Vector2.zero);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        return button;
    }

    private int FindFirstExistingSaveSlot()
    {
        for (int i = 0; i < SlotCount; i++)
            if (ReadSlot(i).exists)
                return i;
        return 0;
    }

    private int FindFirstEmptySaveSlot()
    {
        for (int i = 0; i < SlotCount; i++)
            if (!ReadSlot(i).exists)
                return i;
        return 0;
    }

    private string GetInventorySummary(List<InventoryEntry> entries)
    {
        if (entries == null || entries.Count == 0) return "пусто";

        List<string> descriptions = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null || entry.count <= 0) continue;
            Item item = FindCatalogItem(entry.itemId);
            string itemName = item != null && !string.IsNullOrEmpty(item.itemName)
                ? item.itemName
                : entry.itemId;
            descriptions.Add(itemName + " x" + entry.count);
        }
        return descriptions.Count == 0 ? "пусто" : string.Join(", ", descriptions);
    }

    private static string PassStatus(bool passed)
    {
        return passed ? "пройден" : "не пройден";
    }

    private static string GetNextStageLabel(int stage)
    {
        switch (stage)
        {
            case 0: return "Следующий этап: рогалик";
            case 1: return "Следующий этап: Tower Defence";
            case 2: return "Следующий этап: босс";
            default: return "Игра пройдена";
        }
    }

    private RectTransform CreateRect(string objectName, Transform parent,
        Vector2 size, Vector2 position)
    {
        GameObject element = new GameObject(objectName, typeof(RectTransform));
        element.transform.SetParent(parent, false);
        RectTransform rect = element.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string value,
        Vector2 size, Vector2 position, int fontSize, TextAnchor alignment, FontStyle style)
    {
        RectTransform rect = CreateRect(objectName, parent, size, position);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = menuFontAsset != null ? menuFontAsset : TMP_Settings.defaultFontAsset;
        label.text = value;
        label.fontSize = fontSize;
        label.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = alignment == TextAnchor.MiddleLeft
            ? TextAlignmentOptions.MidlineLeft
            : TextAlignmentOptions.Center;
        label.color = new Color(0.97f, 0.91f, 0.78f, 1f);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(Transform parent, string label, Vector2 size,
        Vector2 position, UnityAction action, Sprite sprite)
    {
        RectTransform rect = CreateRect(label + " button", parent, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.color = sprite != null ? Color.white : new Color(0.42f, 0.27f, 0.15f, 1f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock buttonColors = button.colors;
        buttonColors.normalColor = image.color;
        buttonColors.highlightedColor = Color.Lerp(image.color, Color.white, 0.16f);
        buttonColors.selectedColor = buttonColors.highlightedColor;
        buttonColors.pressedColor = Color.Lerp(image.color, Color.black, 0.16f);
        button.colors = buttonColors;
        button.onClick.AddListener(action);
        Navigation navigation = new Navigation();
        navigation.mode = Navigation.Mode.Automatic;
        button.navigation = navigation;

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI buttonText = CreateText("Button label", rect, label,
                new Vector2(size.x - 12f, size.y - 8f), Vector2.zero, 25,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            buttonText.color = Color.white;
        }
        return button;
    }

    private void SaveTransitionToSlot(int index)
    {
        int resumeStage = screenMode == ScreenMode.SaveAfterRoguelike ? 1 : 2;
        WriteSlot(index, resumeStage);
        BuildScreen(
            screenMode == ScreenMode.SaveAfterRoguelike ? "Рогалик пройден!" : "Tower Defence пройден!",
            "Сохранение записано в слот " + (index + 1) + ". Можно перезаписать другой слот или продолжить.");
    }

    private void WriteSlot(int index, int resumeStage)
    {
        if (index < 0 || index >= SlotCount) return;

        PlayerRunData runData = PlayerRunData.EnsureInstance();
        PlayerHealth health = FindObjectOfType<PlayerHealth>();
        PlayerMana mana = FindObjectOfType<PlayerMana>();

        if (health != null) runData.SaveHealth(health.CurrentHealth);
        if (mana != null) runData.SaveMana(mana.CurrentMana);

        SaveSlotData data = new SaveSlotData();
        data.exists = true;
        data.resumeStage = Mathf.Clamp(resumeStage, 0, 3);
        data.progressPercent = data.resumeStage >= 3 ? 100 : data.resumeStage * 33;
        data.roguelikeCompleted = data.resumeStage >= 1;
        data.towerDefenceCompleted = data.resumeStage >= 2;
        data.bossCompleted = data.resumeStage >= 3;
        data.savedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        data.mushrooms = runData.Mushrooms;
        data.highestUnlockedFloor = runData.HighestUnlockedFloor;
        data.staffDamageLevel = runData.StaffDamageLevel;
        data.manaConsumptionLevel = runData.ManaConsumptionLevel;
        data.manaRegenerationLevel = runData.ManaRegenerationLevel;
        data.maximumHealthLevel = runData.MaximumHealthLevel;
        data.defenseLevel = runData.DefenseLevel;
        data.health = runData.StoredHealth;
        data.mana = runData.StoredMana;
        data.inventory = CaptureInventory();

        PlayerPrefs.SetString(GetSlotKey(index), JsonUtility.ToJson(data));
        activeSlot = index;
        PlayerPrefs.SetInt(ActiveSlotKey, activeSlot);
        PlayerPrefs.Save();
    }

    private List<InventoryEntry> CaptureInventory()
    {
        List<InventoryEntry> result = new List<InventoryEntry>();
        Inventory inventory = Inventory.Instance;
        if (inventory == null) return result;

        for (int i = 0; i < inventory.slots.Count; i++)
        {
            ItemStack stack = inventory.slots[i];
            if (stack == null || stack.IsEmpty || stack.item == null) continue;

            result.Add(new InventoryEntry
            {
                slot = i,
                itemId = stack.item.name,
                count = stack.count
            });
        }
        return result;
    }

    private void LoadSlot(int index)
    {
        SaveSlotData data = ReadSlot(index);
        if (!data.exists) return;

        activeSlot = index;
        PlayerPrefs.SetInt(ActiveSlotKey, activeSlot);
        PlayerPrefs.Save();

        PlayerRunData.EnsureInstance().RestoreRunData(
            data.mushrooms,
            data.highestUnlockedFloor,
            data.roguelikeCompleted,
            data.staffDamageLevel,
            data.manaConsumptionLevel,
            data.manaRegenerationLevel,
            data.maximumHealthLevel,
            data.defenseLevel,
            data.health,
            data.mana);

        pendingInventoryRestore = data.inventory != null
            ? new List<InventoryEntry>(data.inventory)
            : new List<InventoryEntry>();

        string scene = data.resumeStage <= 0 ? "Roguelike"
            : data.resumeStage == 1 ? "TowerDefence"
            : data.resumeStage == 2 ? "Boss" : "End";

        Time.timeScale = 1f;
        CloseCanvasOnly();
        screenMode = ScreenMode.None;
        SceneManager.LoadScene(scene);
    }

    private void DeleteSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return;
        PlayerPrefs.DeleteKey(GetSlotKey(index));
        if (activeSlot == index)
        {
            activeSlot = -1;
            PlayerPrefs.DeleteKey(ActiveSlotKey);
        }
        PlayerPrefs.Save();

        if (screenMode == ScreenMode.Load)
            BuildScreen("Загрузка игры", "Выберите сохранение. В каждом слоте хранится прогресс, грибы, инвентарь и прокачка.");
        else
            BuildScreen(
                screenMode == ScreenMode.SaveAfterRoguelike ? "Рогалик пройден!" : "Tower Defence пройден!",
                "Слот " + (index + 1) + " удалён. Выберите слот для сохранения или продолжите без сохранения.");
    }

    private void ContinueToNextStage()
    {
        if (string.IsNullOrEmpty(nextScene)) return;
        string sceneToLoad = nextScene;
        nextScene = "";
        Time.timeScale = 1f;
        transitionPaused = false;
        CloseCanvasOnly();
        screenMode = ScreenMode.None;
        SceneManager.LoadScene(sceneToLoad);
    }

    private void CloseScreen()
    {
        CloseCanvasOnly();
        screenMode = ScreenMode.None;
        if (transitionPaused)
        {
            Time.timeScale = previousTimeScale;
            transitionPaused = false;
        }
        SetMainMenuEnabled(true);

        GameObject menuObject = GameObject.Find("MainMenuAndLogo");
        Transform startButton = menuObject != null
            ? menuObject.transform.Find("Start Button")
            : null;
        if (startButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    private void SetMainMenuEnabled(bool enabled)
    {
        GameObject menuObject = GameObject.Find("MainMenuAndLogo");
        if (menuObject == null) return;
        ButtonSelectionManager manager = menuObject.GetComponent<ButtonSelectionManager>();
        if (manager != null) manager.enabled = enabled;
        Transform menu = menuObject.transform;
        string[] names = { "Start Button", "Load Game Button", "Options Button", "Exit Button" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform item = menu.Find(names[i]);
            if (item == null) continue;
            Button button = item.GetComponent<Button>();
            if (button != null) button.interactable = enabled;
            ButtonSelectionController selector = item.GetComponent<ButtonSelectionController>();
            if (selector != null) selector.enabled = enabled;
        }
    }

    private void CloseCanvasOnly()
    {
        if (overlayCanvas != null)
        {
            Destroy(overlayCanvas.gameObject);
            overlayCanvas = null;
        }
    }

    private IEnumerator RestoreInventoryAfterSceneLoad()
    {
        yield return null;
        yield return null;

        Inventory inventory = Inventory.EnsureInstance();
        inventory.Init(InventorySize);
        for (int i = 0; i < inventory.slots.Count; i++)
            inventory.slots[i].Clear();

        for (int i = 0; i < pendingInventoryRestore.Count; i++)
        {
            InventoryEntry entry = pendingInventoryRestore[i];
            if (entry == null || entry.count <= 0 ||
                entry.slot < 0 || entry.slot >= inventory.slots.Count)
                continue;

            Item item = FindCatalogItem(entry.itemId);
            if (item == null)
            {
                Debug.LogWarning("Не удалось восстановить предмет из сохранения: " + entry.itemId);
                continue;
            }

            inventory.slots[entry.slot].item = item;
            inventory.slots[entry.slot].count = entry.count;
        }

        inventory.NotifyChanged();
        pendingInventoryRestore = null;
    }

    private Item FindCatalogItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (itemCatalog != null)
        {
            for (int i = 0; i < itemCatalog.Length; i++)
            {
                Item item = itemCatalog[i];
                if (item != null && (item.name == itemId || item.itemName == itemId))
                    return item;
            }
        }

        Item[] loadedItems = Resources.FindObjectsOfTypeAll<Item>();
        for (int i = 0; i < loadedItems.Length; i++)
        {
            Item item = loadedItems[i];
            if (item != null && (item.name == itemId || item.itemName == itemId))
                return item;
        }
        return null;
    }

    private IEnumerator AutoSaveBossCompletion()
    {
        yield return null;
        yield return null;

        int targetSlot = activeSlot;
        if (targetSlot < 0 || targetSlot >= SlotCount)
        {
            targetSlot = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!ReadSlot(i).exists)
                {
                    targetSlot = i;
                    break;
                }
            }
            // Completion must be persisted even when all three slots were occupied.
            if (targetSlot < 0) targetSlot = 0;
        }

        WriteSlot(targetSlot, 3);
        Debug.Log("Прохождение босса автоматически сохранено в слот " + (targetSlot + 1));
    }

    private SaveSlotData ReadSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return new SaveSlotData();
        string json = PlayerPrefs.GetString(GetSlotKey(index), "");
        if (string.IsNullOrEmpty(json)) return new SaveSlotData();

        SaveSlotData data = JsonUtility.FromJson<SaveSlotData>(json);
        if (data == null) return new SaveSlotData();
        if (data.inventory == null) data.inventory = new List<InventoryEntry>();
        return data;
    }

    private static string GetSlotKey(int index)
    {
        return "WizardFroggy.SaveSlot." + index;
    }
}
