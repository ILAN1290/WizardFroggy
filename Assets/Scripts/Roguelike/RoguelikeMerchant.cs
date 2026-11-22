using UnityEngine;

public class RoguelikeMerchant : MonoBehaviour
{
    private RoguelikeSession session;
    private readonly Item[] offeredItems = new Item[3];
    private int selectedOfferIndex;
    private GUIStyle titleStyle;
    private GUIStyle normalStyle;
    private GUIStyle smallStyle;

    public void Initialize(RoguelikeSession owner)
    {
        session = owner;
        GenerateOffers();
    }

    private void GenerateOffers()
    {
        Item[] available = Resources.LoadAll<Item>("RoguelikeLoot");
        for (int i = 0; i < offeredItems.Length; i++)
            offeredItems[i] = null;

        if (available == null || available.Length == 0)
            return;

        // Shuffle once when this merchant is created so all three slots are unique.
        Item[] shuffled = (Item[])available.Clone();
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            Item temp = shuffled[i];
            shuffled[i] = shuffled[swapIndex];
            shuffled[swapIndex] = temp;
        }

        for (int i = 0; i < offeredItems.Length && i < shuffled.Length; i++)
            offeredItems[i] = shuffled[i];

        selectedOfferIndex = 0;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        bool pressed = InputManager.instance != null
            ? InputManager.instance.InteractionInput
            : UnityEngine.Input.GetKeyDown(KeyCode.E);
        if (!pressed) return;

        if (session != null)
        {
            session.MarkCurrentRoomCleared();
            session.OpenShop(this);
        }
    }

    public void DrawShopGUI(RoguelikeSession owner)
    {
        PlayerRunData data = PlayerRunData.EnsureInstance();
        EnsureStyles();

        // The panel covers roughly 60% of the screen area while remaining responsive.
        float panelWidth = Screen.width * 0.76f;
        float panelHeight = Screen.height * 0.80f;
        panelWidth = Mathf.Min(panelWidth, Screen.width - 24f);
        panelHeight = Mathf.Min(panelHeight, Screen.height - 24f);

        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth, panelHeight);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.20f, 0.12f, 0.075f, 0.98f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = previousColor;

        float margin = panel.width * 0.035f;
        float headerHeight = panel.height * 0.12f;
        GUI.Label(new Rect(panel.x + margin, panel.y + 10f,
            panel.width * 0.58f, headerHeight - 8f),
            "ЛАВКА СТАРОГО БИБЛИОТЕКАРЯ", titleStyle);
        GUI.Label(new Rect(panel.x + panel.width * 0.65f, panel.y + 15f,
            panel.width * 0.23f, 30f),
            "Грибочки: " + data.Mushrooms, normalStyle);

        Rect closeRect = new Rect(panel.xMax - margin - 44f,
            panel.y + 10f, 40f, 36f);
        if (GUI.Button(closeRect, "×"))
            owner.CloseShop();

        DrawItemOffers(panel, margin, headerHeight);
        DrawSelectedItemDescription(panel, margin, headerHeight);

        float upgradeTop = panel.y + panel.height * 0.49f;
        float bottomMargin = panel.height * 0.035f;
        float rowGap = panel.height * 0.018f;
        float availableHeight = panel.yMax - bottomMargin - upgradeTop;
        float rowHeight = (availableHeight - rowGap * 2f) / 3f;

        DrawUpgradeRow(data, panel, margin, upgradeTop, rowHeight,
            RoguelikeUpgradeType.StaffDamage, "УРОН ПОСОХА",
            "Урон: " + data.StaffDamage.ToString("0.0"));
        DrawUpgradeRow(data, panel, margin, upgradeTop + rowHeight + rowGap,
            rowHeight, RoguelikeUpgradeType.Mana, "МАНА",
            "Расход: " + data.ProjectileManaCost.ToString("0.0") +
            "  •  Регенерация: " + data.ManaRegeneration.ToString("0.0") + "/с");
        DrawUpgradeRow(data, panel, margin, upgradeTop + (rowHeight + rowGap) * 2f,
            rowHeight, RoguelikeUpgradeType.Vitality, "ЗДОРОВЬЕ И ЗАЩИТА",
            "Здоровье: " + data.MaximumHealth.ToString("0") +
            "  •  Защита: " + (data.DamageReduction * 100f).ToString("0") + "%");
    }

    private void DrawItemOffers(Rect panel, float margin, float headerHeight)
    {
        float top = panel.y + headerHeight;
        float gap = panel.width * 0.018f;
        float slotWidth = (panel.width - margin * 2f - gap * 2f) / 3f;
        float slotHeight = panel.height * 0.205f;

        for (int i = 0; i < offeredItems.Length; i++)
        {
            Rect slot = new Rect(panel.x + margin + i * (slotWidth + gap),
                top, slotWidth, slotHeight);

            Color previousColor = GUI.color;
            GUI.color = i == selectedOfferIndex
                ? new Color(0.62f, 0.39f, 0.18f, 1f)
                : new Color(0.31f, 0.22f, 0.16f, 1f);
            GUI.Box(slot, GUIContent.none);
            GUI.color = previousColor;

            Item item = offeredItems[i];
            if (item == null)
            {
                GUI.Label(new Rect(slot.x, slot.y + slot.height * 0.4f,
                    slot.width, 28f), "ПРОДАНО", normalStyle);
                continue;
            }

            Rect iconRect = new Rect(slot.x + slot.width * 0.36f,
                slot.y + 8f, slot.width * 0.28f, slot.height * 0.56f);
            DrawItemIcon(item.icon, iconRect);

            GUI.Label(new Rect(slot.x + 8f, slot.y + slot.height * 0.67f,
                slot.width - 16f, 24f), item.itemName, normalStyle);
            GUI.Label(new Rect(slot.x + 8f, slot.y + slot.height * 0.83f,
                slot.width - 16f, 22f), GetItemPrice(item) + " гриб.",
                smallStyle);

            if (GUI.Button(slot, GUIContent.none, GUIStyle.none))
                selectedOfferIndex = i;
        }
    }

    private void DrawSelectedItemDescription(Rect panel, float margin, float headerHeight)
    {
        float top = panel.y + headerHeight + panel.height * 0.225f;
        float height = panel.height * 0.125f;
        Rect descriptionRect = new Rect(panel.x + margin, top,
            panel.width - margin * 2f, height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.13f, 0.085f, 0.06f, 0.95f);
        GUI.Box(descriptionRect, GUIContent.none);
        GUI.color = previousColor;

        Item selected = selectedOfferIndex >= 0 && selectedOfferIndex < offeredItems.Length
            ? offeredItems[selectedOfferIndex] : null;

        if (selected == null)
        {
            GUI.Label(new Rect(descriptionRect.x + 16f, descriptionRect.y + 10f,
                descriptionRect.width - 32f, 28f),
                "Выберите доступный предмет в одном из трёх слотов.", normalStyle);
            return;
        }

        GUI.Label(new Rect(descriptionRect.x + 16f, descriptionRect.y + 8f,
            descriptionRect.width * 0.62f, 28f),
            selected.itemName, normalStyle);
        GUI.Label(new Rect(descriptionRect.x + 16f, descriptionRect.y + 36f,
            descriptionRect.width * 0.66f, height - 40f),
            GetItemDescription(selected), smallStyle);

        int price = GetItemPrice(selected);
        bool canBuy = PlayerRunData.EnsureInstance().Mushrooms >= price &&
            Inventory.Instance != null && Inventory.Instance.CanAdd(selected, 1);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && canBuy;
        if (GUI.Button(new Rect(descriptionRect.xMax - 220f,
            descriptionRect.y + (height - 42f) * 0.5f, 200f, 42f),
            "КУПИТЬ • " + price))
            BuySelectedItem();
        GUI.enabled = previousEnabled;
    }

    private void DrawUpgradeRow(PlayerRunData data, Rect panel, float margin,
        float y, float height, RoguelikeUpgradeType type, string title, string value)
    {
        Rect row = new Rect(panel.x + margin, y,
            panel.width - margin * 2f, height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.12f, 0.075f, 0.055f, 0.9f);
        GUI.Box(row, GUIContent.none);
        GUI.color = previousColor;

        int level = data.GetUpgradeLevel(type);
        int cost = data.GetUpgradePrice(type);
        int refund = data.GetUpgradeRefund(type);

        GUI.Label(new Rect(row.x + 14f, row.y + 5f,
            row.width * 0.25f, 24f), title, normalStyle);
        GUI.Label(new Rect(row.x + 14f, row.y + 29f,
            row.width * 0.25f, 22f), value, smallStyle);

        float buttonWidth = Mathf.Clamp(row.width * 0.055f, 34f, 58f);
        float minusX = row.x + row.width * 0.29f;
        Rect minusRect = new Rect(minusX, row.y + (height - 34f) * 0.5f,
            buttonWidth, 34f);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && data.CanDowngrade(type);
        if (GUI.Button(minusRect, "−"))
        {
            if (data.TryDowngrade(type))
            {
                if (type == RoguelikeUpgradeType.Vitality)
                    ApplyVitalityChange();
                ShowHint("Уровень снижен. Возвращено " + refund + " грибочков.");
            }
        }

        float plusX = row.x + row.width - buttonWidth;
        Rect plusRect = new Rect(plusX, row.y + (height - 34f) * 0.5f,
            buttonWidth, 34f);
        GUI.enabled = previousEnabled && data.CanUpgrade(type) && data.Mushrooms >= cost;
        if (GUI.Button(plusRect, "+"))
        {
            if (data.TryUpgrade(type))
            {
                if (type == RoguelikeUpgradeType.Vitality)
                    ApplyVitalityChange();
                ShowHint("Уровень улучшения: " + data.GetUpgradeLevel(type) + "/10.");
            }
            else
            {
                ShowHint("Не хватает грибочков.");
            }
        }
        GUI.enabled = previousEnabled;

        float meterX = minusRect.xMax + 12f;
        float meterWidth = plusRect.xMin - meterX - 12f;
        float segmentGap = Mathf.Clamp(meterWidth * 0.012f, 2f, 6f);
        float segmentWidth = (meterWidth - segmentGap * 9f) / 10f;
        float segmentHeight = Mathf.Min(26f, height * 0.42f);
        float segmentY = row.y + (height - segmentHeight) * 0.5f;

        for (int i = 0; i < 10; i++)
        {
            Rect segment = new Rect(meterX + i * (segmentWidth + segmentGap),
                segmentY, segmentWidth, segmentHeight);
            GUI.color = i < level
                ? new Color(0.92f, 0.68f, 0.25f, 1f)
                : new Color(0.25f, 0.19f, 0.14f, 1f);
            GUI.DrawTexture(segment, Texture2D.whiteTexture);
        }
        GUI.color = previousColor;

        GUI.Label(new Rect(meterX, row.y + 4f, meterWidth, 20f),
            level + "/10", smallStyle);
        GUI.Label(new Rect(minusRect.x - 4f, row.y + height - 22f,
            buttonWidth + 8f, 18f), "½ " + refund, smallStyle);
        GUI.Label(new Rect(plusRect.x - 18f, row.y + height - 22f,
            buttonWidth + 36f, 18f), "+" + cost, smallStyle);
    }

    private void BuySelectedItem()
    {
        if (selectedOfferIndex < 0 || selectedOfferIndex >= offeredItems.Length) return;

        Item item = offeredItems[selectedOfferIndex];
        if (item == null) return;

        PlayerRunData data = PlayerRunData.EnsureInstance();
        int price = GetItemPrice(item);

        if (Inventory.Instance == null || !Inventory.Instance.CanAdd(item, 1))
        {
            ShowHint("Нет свободного места в инвентаре.");
            return;
        }

        if (!data.SpendMushrooms(price))
        {
            ShowHint("Не хватает грибочков.");
            return;
        }

        Inventory.Instance.Add(item, 1);
        offeredItems[selectedOfferIndex] = null;
        SelectNextAvailableOffer();
        ShowHint("Куплено: " + item.itemName);
    }

    private void SelectNextAvailableOffer()
    {
        for (int offset = 0; offset < offeredItems.Length; offset++)
        {
            int index = (selectedOfferIndex + offset + 1) % offeredItems.Length;
            if (offeredItems[index] != null)
            {
                selectedOfferIndex = index;
                return;
            }
        }
    }

    private void ApplyVitalityChange()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerHealth health = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (health != null)
            health.ApplyMaximumHealthUpgrade();
    }

    private static string GetItemDescription(Item item)
    {
        if (item is HealingPotion) return "Восстанавливает здоровье при использовании.";
        if (item is ManaPotion) return "Восполняет запас маны.";
        if (item is StrengthPotion) return "Временно увеличивает наносимый урон.";
        if (item is SpeedPotion) return "Временно повышает скорость передвижения.";
        if (item is InvincibilityPotion) return "На время защищает от входящего урона.";
        if (item is InfiniteManaPotion) return "На время даёт неограниченный запас маны.";
        return "Полезный предмет для дальнейшего прохождения.";
    }

    private static void DrawItemIcon(Sprite sprite, Rect destination)
    {
        if (sprite == null || sprite.texture == null) return;

        Texture2D texture = sprite.texture;
        Rect source = sprite.textureRect;
        Rect uv = new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height);
        GUI.DrawTextureWithTexCoords(destination, texture, uv, true);
    }

    private void EnsureStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 38, 18, 30),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(1f, 0.87f, 0.66f);
        }

        if (normalStyle == null)
        {
            normalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 55, 13, 21),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            normalStyle.normal.textColor = new Color(1f, 0.93f, 0.82f);
        }

        if (smallStyle == null)
        {
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 72, 11, 16),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            smallStyle.normal.textColor = new Color(0.86f, 0.79f, 0.68f);
        }
    }

    private int GetItemPrice(Item item)
    {
        if (item is HealingPotion) return 18;
        if (item is ManaPotion) return 16;
        if (item is StrengthPotion) return 24;
        if (item is SpeedPotion) return 20;
        if (item is InvincibilityPotion) return 28;
        if (item is InfiniteManaPotion) return 32;
        return 10;
    }

    private void ShowHint(string message)
    {
        if (session != null)
            session.ShowHint(message);
    }
}