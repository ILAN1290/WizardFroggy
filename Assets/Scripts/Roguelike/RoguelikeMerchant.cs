using UnityEngine;

public class RoguelikeMerchant : MonoBehaviour
{
    private static readonly RoguelikeUpgradeType[] UpgradeTypes =
    {
        RoguelikeUpgradeType.StaffDamage,
        RoguelikeUpgradeType.ManaConsumption,
        RoguelikeUpgradeType.ManaRegeneration,
        RoguelikeUpgradeType.MaximumHealth,
        RoguelikeUpgradeType.Defense
    };

    private RoguelikeSession session;
    private RoguelikeMerchantTextures textures;
    private readonly Item[] offeredItems = new Item[3];
    private int selectedOfferIndex;
    private GUIStyle titleStyle;
    private GUIStyle normalStyle;
    private GUIStyle smallStyle;

    public void Initialize(RoguelikeSession owner, RoguelikeMerchantTextures uiTextures)
    {
        session = owner;
        textures = uiTextures;
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

        // 76% of screen width x 80% height is approximately 61% of screen area.
        float panelWidth = Mathf.Min(Screen.width * 0.76f, Screen.width - 24f);
        float panelHeight = Mathf.Min(Screen.height * 0.80f, Screen.height - 24f);
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth, panelHeight);

        DrawPanel(panel, textures != null ? textures.panelBackground : null,
            new Color(0.20f, 0.12f, 0.075f, 0.98f));

        float margin = panel.width * 0.035f;
        float headerHeight = panel.height * 0.12f;
        GUI.Label(new Rect(panel.x + margin, panel.y + 8f,
            panel.width * 0.58f, headerHeight - 8f),
            "ЛАВКА СТАРОГО БИБЛИОТЕКАРЯ", titleStyle);

        float currencyX = panel.x + panel.width * 0.65f;
        if (textures != null && textures.currencyIcon != null)
        {
            Rect currencyRect = new Rect(currencyX, panel.y + 13f, 28f, 28f);
            GUI.DrawTexture(currencyRect, textures.currencyIcon, ScaleMode.ScaleToFit, true);
            currencyX += 34f;
        }
        GUI.Label(new Rect(currencyX, panel.y + 12f,
            panel.width * 0.20f, 30f), "Грибочки: " + data.Mushrooms, normalStyle);

        Rect closeRect = new Rect(panel.xMax - margin - 44f,
            panel.y + 9f, 40f, 36f);
        if (DrawSkinnedButton(closeRect, "×",
            textures != null ? textures.closeButton : null, normalStyle))
            owner.CloseShop();

        DrawItemOffers(panel, margin, headerHeight);
        DrawSelectedItemDescription(panel, margin, headerHeight);

        // Five independent tracks: staff damage, mana cost, mana regeneration,
        // maximum health and defence.
        float upgradeTop = panel.y + panel.height * 0.49f;
        float bottomMargin = panel.height * 0.035f;
        float rowGap = panel.height * 0.009f;
        float availableHeight = panel.yMax - bottomMargin - upgradeTop;
        float rowHeight = (availableHeight - rowGap * (UpgradeTypes.Length - 1)) /
            UpgradeTypes.Length;

        for (int i = 0; i < UpgradeTypes.Length; i++)
        {
            float y = upgradeTop + i * (rowHeight + rowGap);
            RoguelikeUpgradeType type = UpgradeTypes[i];
            DrawUpgradeRow(data, panel, margin, y, rowHeight, type,
                GetUpgradeTitle(type), GetUpgradeValue(data, type));
        }
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

            Item item = offeredItems[i];
            Texture2D slotTexture = textures == null ? null :
                item == null ? textures.itemSlotSoldBackground :
                i == selectedOfferIndex ? textures.itemSlotSelectedBackground :
                textures.itemSlotBackground;

            Color fallback = item == null
                ? new Color(0.18f, 0.14f, 0.12f, 1f)
                : i == selectedOfferIndex
                    ? new Color(0.62f, 0.39f, 0.18f, 1f)
                    : new Color(0.31f, 0.22f, 0.16f, 1f);
            DrawPanel(slot, slotTexture, fallback);

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

        DrawPanel(descriptionRect,
            textures != null ? textures.itemDescriptionBackground : null,
            new Color(0.13f, 0.085f, 0.06f, 0.95f));

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
        if (DrawSkinnedButton(new Rect(descriptionRect.xMax - 220f,
            descriptionRect.y + (height - 42f) * 0.5f, 200f, 42f),
            "КУПИТЬ • " + price,
            textures != null ? textures.itemBuyButton : null, normalStyle))
            BuySelectedItem();
        GUI.enabled = previousEnabled;
    }

    private void DrawUpgradeRow(PlayerRunData data, Rect panel, float margin,
        float y, float height, RoguelikeUpgradeType type, string title, string value)
    {
        Rect row = new Rect(panel.x + margin, y,
            panel.width - margin * 2f, height);
        DrawPanel(row, textures != null ? textures.upgradeRowBackground : null,
            new Color(0.12f, 0.075f, 0.055f, 0.9f));

        int level = data.GetUpgradeLevel(type);
        int cost = data.GetUpgradePrice(type);
        int refund = data.GetUpgradeRefund(type);

        GUI.Label(new Rect(row.x + 14f, row.y + 3f,
            row.width * 0.25f, 24f), title, normalStyle);
        GUI.Label(new Rect(row.x + 14f, row.y + 28f,
            row.width * 0.25f, 22f), value, smallStyle);

        float buttonWidth = Mathf.Clamp(row.width * 0.055f, 34f, 58f);
        float buttonHeight = Mathf.Min(34f, height * 0.52f);
        float buttonY = row.y + (height - buttonHeight) * 0.5f;
        float minusX = row.x + row.width * 0.29f;
        Rect minusRect = new Rect(minusX, buttonY, buttonWidth, buttonHeight);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && data.CanDowngrade(type);
        if (DrawSkinnedButton(minusRect, "−",
            textures != null ? textures.upgradeMinusButton : null, normalStyle))
        {
            if (data.TryDowngrade(type))
            {
                if (type == RoguelikeUpgradeType.MaximumHealth)
                    ApplyMaximumHealthChange();
                ShowHint("Уровень снижен. Возвращено " + refund + " грибочков.");
            }
        }

        float plusX = row.x + row.width - buttonWidth;
        Rect plusRect = new Rect(plusX, buttonY, buttonWidth, buttonHeight);
        GUI.enabled = previousEnabled && data.CanUpgrade(type) && data.Mushrooms >= cost;
        if (DrawSkinnedButton(plusRect, "+",
            textures != null ? textures.upgradePlusButton : null, normalStyle))
        {
            if (data.TryUpgrade(type))
            {
                if (type == RoguelikeUpgradeType.MaximumHealth)
                    ApplyMaximumHealthChange();
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
        float segmentWidth = Mathf.Max(1f, (meterWidth - segmentGap * 9f) / 10f);
        float segmentHeight = Mathf.Min(24f, height * 0.34f);
        float segmentY = row.y + (height - segmentHeight) * 0.5f;

        for (int i = 0; i < 10; i++)
        {
            Rect segment = new Rect(meterX + i * (segmentWidth + segmentGap),
                segmentY, segmentWidth, segmentHeight);
            Texture2D segmentTexture = null;
            if (textures != null)
                segmentTexture = i < level
                    ? textures.upgradeLevelFilled : textures.upgradeLevelEmpty;

            Color fallback = i < level
                ? new Color(0.92f, 0.68f, 0.25f, 1f)
                : new Color(0.25f, 0.19f, 0.14f, 1f);
            DrawPanel(segment, segmentTexture, fallback);
        }

        GUI.Label(new Rect(meterX, row.y + 2f, meterWidth, 18f),
            level + "/10", smallStyle);
        GUI.Label(new Rect(minusRect.x - 4f, row.y + height - 19f,
            buttonWidth + 8f, 17f), "½ " + refund, smallStyle);
        GUI.Label(new Rect(plusRect.x - 18f, row.y + height - 19f,
            buttonWidth + 36f, 17f), "+" + cost, smallStyle);
    }

    private static string GetUpgradeTitle(RoguelikeUpgradeType type)
    {
        switch (type)
        {
            case RoguelikeUpgradeType.StaffDamage: return "УРОН ПОСОХА";
            case RoguelikeUpgradeType.ManaConsumption: return "РАСХОД МАНЫ";
            case RoguelikeUpgradeType.ManaRegeneration: return "РЕГЕНЕРАЦИЯ МАНЫ";
            case RoguelikeUpgradeType.MaximumHealth: return "МАКСИМАЛЬНОЕ ЗДОРОВЬЕ";
            case RoguelikeUpgradeType.Defense: return "ЗАЩИТА";
            default: return "УЛУЧШЕНИЕ";
        }
    }

    private static string GetUpgradeValue(PlayerRunData data, RoguelikeUpgradeType type)
    {
        switch (type)
        {
            case RoguelikeUpgradeType.StaffDamage:
                return "Урон: " + data.StaffDamage.ToString("0.0");
            case RoguelikeUpgradeType.ManaConsumption:
                return "За выстрел: " + data.ProjectileManaCost.ToString("0.0");
            case RoguelikeUpgradeType.ManaRegeneration:
                return "В секунду: " + data.ManaRegeneration.ToString("0.0");
            case RoguelikeUpgradeType.MaximumHealth:
                return "Здоровье: " + data.MaximumHealth.ToString("0");
            case RoguelikeUpgradeType.Defense:
                return "Снижение урона: " + (data.DamageReduction * 100f).ToString("0") + "%";
            default:
                return "—";
        }
    }

    private static void DrawPanel(Rect rect, Texture2D texture, Color fallbackColor)
    {
        if (texture != null)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            return;
        }

        Color previousColor = GUI.color;
        GUI.color = fallbackColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private static bool DrawSkinnedButton(
        Rect rect, string label, Texture2D texture, GUIStyle labelStyle)
    {
        if (texture == null)
            return GUI.Button(rect, label);

        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        GUI.Label(rect, label, labelStyle);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
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

    private void ApplyMaximumHealthChange()
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