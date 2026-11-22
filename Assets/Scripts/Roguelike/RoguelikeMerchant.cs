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
    private GUIStyle vendorStyle;
    private GUIStyle normalStyle;
    private GUIStyle smallStyle;
    private Sprite currencyIcon;

    public void Initialize(RoguelikeSession owner,
        RoguelikeMerchantTextures uiTextures, Sprite fallbackCurrencyIcon = null)
    {
        session = owner;
        textures = uiTextures;
        currencyIcon = uiTextures != null && uiTextures.currencyIcon != null
            ? uiTextures.currencyIcon : fallbackCurrencyIcon;
        GenerateOffers();
    }

    private void GenerateOffers()
    {
        Item[] available = Resources.LoadAll<Item>("RoguelikeLoot");
        for (int i = 0; i < offeredItems.Length; i++)
            offeredItems[i] = null;

        if (available == null || available.Length == 0)
            return;

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

        // Keep the complete shop narrower. The merchant occupies the left third;
        // the two actual UI sections are shifted right into the remaining two thirds.
        float panelWidth = Mathf.Min(Screen.width * 0.70f, Screen.width - 24f);
        float panelHeight = Mathf.Min(Screen.height * 0.82f, Screen.height - 24f);
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth, panelHeight);

        DrawPanel(panel, textures != null ? textures.panelBackground : null,
            new Color(0.20f, 0.12f, 0.075f, 0.98f));

        float outerMargin = panel.width * 0.025f;
        float vendorWidth = panel.width * 0.285f;
        Rect vendorRect = new Rect(panel.x + outerMargin,
            panel.y + outerMargin,
            vendorWidth, panel.height - outerMargin * 2f);
        DrawVendor(vendorRect);

        float contentX = panel.x + panel.width * 0.325f;
        float contentWidth = panel.width * 0.65f;
        float headerHeight = panel.height * 0.105f;
        float contentRight = contentX + contentWidth;

        GUI.Label(new Rect(contentX + 4f, panel.y + 8f,
            contentWidth * 0.52f, headerHeight - 10f),
            "ТОРГОВАЯ ЛАВКА", titleStyle);

        // Mushroom icon is a Sprite and is drawn at the same pixel height as its count.
        float currencyIconSide = normalStyle.fontSize + 5f;
        float currencyX = contentRight - 200f;
        Rect currencyIconRect = new Rect(currencyX,
            panel.y + (headerHeight - currencyIconSide) * 0.5f,
            currencyIconSide, currencyIconSide);
        DrawSpriteFit(currencyIcon, currencyIconRect);

        GUI.Label(new Rect(currencyIconRect.xMax + 7f,
            panel.y + 6f, 92f, headerHeight - 8f),
            data.Mushrooms.ToString(), normalStyle);

        float closeSide = Mathf.Min(44f, headerHeight - 8f);
        Rect closeRect = new Rect(contentRight - closeSide,
            panel.y + (headerHeight - closeSide) * 0.5f,
            closeSide, closeSide);
        if (DrawSquareButton(closeRect, "×",
            textures != null ? textures.closeButton : null))
            owner.CloseShop();

        float slotsTop = panel.y + headerHeight + 7f;
        float slotsGap = contentWidth * 0.025f;
        float slotSide = Mathf.Min(
            (contentWidth - slotsGap * 2f) / 3f,
            panelHeight * 0.235f);
        float slotsWidth = slotSide * 3f + slotsGap * 2f;
        float slotsLeft = contentX + (contentWidth - slotsWidth) * 0.5f;

        DrawItemOffers(slotsLeft, slotsTop, slotSide, slotsGap);

        float descriptionTop = slotsTop + slotSide + panel.height * 0.014f;
        float descriptionHeight = panel.height * 0.115f;
        DrawSelectedItemDescription(new Rect(contentX, descriptionTop,
            contentWidth, descriptionHeight));

        // The five upgrade rows use square +/- buttons and ten square level cells.
        float upgradeTop = panel.y + panel.height * 0.505f;
        float bottomMargin = panel.height * 0.025f;
        float rowGap = panel.height * 0.008f;
        float rowsHeight = panel.yMax - bottomMargin - upgradeTop;
        float rowHeight = (rowsHeight - rowGap * (UpgradeTypes.Length - 1)) /
            UpgradeTypes.Length;

        for (int i = 0; i < UpgradeTypes.Length; i++)
        {
            float y = upgradeTop + i * (rowHeight + rowGap);
            RoguelikeUpgradeType type = UpgradeTypes[i];
            DrawUpgradeRow(data, new Rect(contentX, y, contentWidth, rowHeight),
                type, GetUpgradeTitle(type), GetUpgradeValue(data, type));
        }
    }

    private void DrawVendor(Rect vendorRect)
    {
        DrawPanel(vendorRect,
            textures != null ? textures.vendorPanelBackground : null,
            new Color(0.13f, 0.075f, 0.05f, 0.96f));

        GUI.Label(new Rect(vendorRect.x + 8f, vendorRect.y + 12f,
            vendorRect.width - 16f, 46f), "ТОРГОВЕЦ", vendorStyle);

        float portraitWidth = vendorRect.width * 0.88f;
        float portraitHeight = vendorRect.height * 0.66f;
        Rect portraitRect = new Rect(
            vendorRect.center.x - portraitWidth * 0.5f,
            vendorRect.y + vendorRect.height * 0.14f,
            portraitWidth, portraitHeight);

        if (textures != null && textures.merchantPortrait != null)
            DrawSpriteFit(textures.merchantPortrait, portraitRect);
        else
            GUI.Label(portraitRect, "Портрет торговца", normalStyle);

        GUI.Label(new Rect(vendorRect.x + 12f, portraitRect.yMax + 14f,
            vendorRect.width - 24f, 64f),
            "Старый библиотекарь", normalStyle);
        GUI.Label(new Rect(vendorRect.x + 14f, portraitRect.yMax + 78f,
            vendorRect.width - 28f, vendorRect.yMax - portraitRect.yMax - 90f),
            "Редкие вещи и улучшения для долгого путешествия.", smallStyle);
    }

    private void DrawItemOffers(float left, float top, float slotSide, float gap)
    {
        for (int i = 0; i < offeredItems.Length; i++)
        {
            Rect slot = new Rect(left + i * (slotSide + gap),
                top, slotSide, slotSide);

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
                GUI.Label(new Rect(slot.x + 5f, slot.center.y - 15f,
                    slot.width - 10f, 30f), "ПРОДАНО", normalStyle);
                continue;
            }

            float iconSide = slotSide * 0.48f;
            Rect iconRect = new Rect(slot.center.x - iconSide * 0.5f,
                slot.y + slotSide * 0.08f, iconSide, iconSide);
            DrawSpriteFit(item.icon, iconRect);

            GUI.Label(new Rect(slot.x + 7f, slot.y + slotSide * 0.62f,
                slot.width - 14f, slotSide * 0.20f),
                item.itemName, normalStyle);
            GUI.Label(new Rect(slot.x + 7f, slot.y + slotSide * 0.82f,
                slot.width - 14f, slotSide * 0.15f),
                GetItemPrice(item) + " гриб.", smallStyle);

            if (GUI.Button(slot, GUIContent.none, GUIStyle.none))
                selectedOfferIndex = i;
        }
    }

    private void DrawSelectedItemDescription(Rect rect)
    {
        DrawPanel(rect,
            textures != null ? textures.itemDescriptionBackground : null,
            new Color(0.13f, 0.085f, 0.06f, 0.95f));

        Item selected = selectedOfferIndex >= 0 && selectedOfferIndex < offeredItems.Length
            ? offeredItems[selectedOfferIndex] : null;

        if (selected == null)
        {
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f,
                rect.width - 24f, rect.height - 12f),
                "Выберите предмет в одном из трёх слотов.", normalStyle);
            return;
        }

        float buySide = Mathf.Min(rect.height - 10f, 76f);
        Rect buyRect = new Rect(rect.xMax - buySide - 8f,
            rect.center.y - buySide * 0.5f, buySide, buySide);

        GUI.Label(new Rect(rect.x + 12f, rect.y + 5f,
            rect.width - buySide - 38f, 30f),
            selected.itemName, normalStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 35f,
            rect.width - buySide - 38f, rect.height - 40f),
            GetItemDescription(selected), smallStyle);

        int price = GetItemPrice(selected);
        bool canBuy = PlayerRunData.EnsureInstance().Mushrooms >= price &&
            Inventory.Instance != null && Inventory.Instance.CanAdd(selected, 1);
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && canBuy;
        if (DrawSquareButton(buyRect, price.ToString(),
            textures != null ? textures.itemBuyButton : null))
            BuySelectedItem();
        GUI.enabled = previousEnabled;
    }

    private void DrawUpgradeRow(PlayerRunData data, Rect row,
        RoguelikeUpgradeType type, string title, string value)
    {
        DrawPanel(row, textures != null ? textures.upgradeRowBackground : null,
            new Color(0.12f, 0.075f, 0.055f, 0.9f));

        int level = data.GetUpgradeLevel(type);
        int cost = data.GetUpgradePrice(type);
        int refund = data.GetUpgradeRefund(type);

        float buttonSide = Mathf.Clamp(row.height * 0.60f, 30f, 46f);
        float buttonY = row.center.y - buttonSide * 0.5f;
        Rect plusRect = new Rect(row.xMax - buttonSide - 4f,
            buttonY, buttonSide, buttonSide);
        Rect minusRect = new Rect(plusRect.x - buttonSide - 8f,
            buttonY, buttonSide, buttonSide);

        float labelWidth = row.width * 0.28f;
        GUI.Label(new Rect(row.x + 10f, row.y + 3f,
            labelWidth, row.height * 0.48f), title, normalStyle);
        GUI.Label(new Rect(row.x + 10f, row.y + row.height * 0.48f,
            labelWidth, row.height * 0.40f), value, smallStyle);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && data.CanDowngrade(type);
        if (DrawSquareButton(minusRect, "−",
            textures != null ? textures.upgradeMinusButton : null))
        {
            if (data.TryDowngrade(type))
            {
                if (type == RoguelikeUpgradeType.MaximumHealth)
                    ApplyMaximumHealthChange();
                ShowHint("Уровень снижен. Возвращено " + refund + " грибочков.");
            }
        }

        GUI.enabled = previousEnabled && data.CanUpgrade(type) && data.Mushrooms >= cost;
        if (DrawSquareButton(plusRect, "+",
            textures != null ? textures.upgradePlusButton : null))
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

        // Reserve a wide strip between the stat label and the two square buttons.
        // Each of the ten level cells is square rather than a stretched rectangle.
        float meterX = row.x + labelWidth + 12f;
        float meterRight = minusRect.x - 12f;
        float meterWidth = Mathf.Max(0f, meterRight - meterX);
        float segmentGap = 3f;
        float segmentSide = Mathf.Min(
            row.height * 0.42f,
            (meterWidth - segmentGap * 9f) / 10f);
        segmentSide = Mathf.Max(1f, segmentSide);
        float totalMeterWidth = segmentSide * 10f + segmentGap * 9f;
        meterX += Mathf.Max(0f, (meterWidth - totalMeterWidth) * 0.5f);
        float meterY = row.center.y - segmentSide * 0.5f;

        for (int i = 0; i < 10; i++)
        {
            Rect segment = new Rect(meterX + i * (segmentSide + segmentGap),
                meterY, segmentSide, segmentSide);
            Texture2D segmentTexture = null;
            if (textures != null)
                segmentTexture = i < level
                    ? textures.upgradeLevelFilled : textures.upgradeLevelEmpty;

            Color fallback = i < level
                ? new Color(0.92f, 0.68f, 0.25f, 1f)
                : new Color(0.25f, 0.19f, 0.14f, 1f);
            DrawPanel(segment, segmentTexture, fallback);
        }

        GUI.Label(new Rect(minusRect.x - 12f, row.y + row.height - 20f,
            buttonSide + 24f, 18f), "½ " + refund, smallStyle);
        GUI.Label(new Rect(plusRect.x - 12f, row.y + row.height - 20f,
            buttonSide + 24f, 18f), "+" + cost, smallStyle);
    }

    private static string GetUpgradeTitle(RoguelikeUpgradeType type)
    {
        switch (type)
        {
            case RoguelikeUpgradeType.StaffDamage: return "УРОН ПОСОХА";
            case RoguelikeUpgradeType.ManaConsumption: return "РАСХОД МАНЫ";
            case RoguelikeUpgradeType.ManaRegeneration: return "РЕГЕНЕРАЦИЯ МАНЫ";
            case RoguelikeUpgradeType.MaximumHealth: return "МАКС. ЗДОРОВЬЕ";
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
                return "HP: " + data.MaximumHealth.ToString("0");
            case RoguelikeUpgradeType.Defense:
                return "Защита: " + (data.DamageReduction * 100f).ToString("0") + "%";
            default:
                return "—";
        }
    }

    private static void DrawPanel(Rect rect, Texture2D texture, Color fallbackColor)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
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

    private bool DrawSquareButton(Rect rect, string label, Texture2D texture)
    {
        if (texture != null)
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);

        GUIStyle buttonTextStyle = normalStyle;
        if (texture == null)
            return GUI.Button(rect, label);

        GUI.Label(rect, label, buttonTextStyle);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private static void DrawSpriteFit(Sprite sprite, Rect destination)
    {
        if (sprite == null || sprite.texture == null) return;

        Texture2D texture = sprite.texture;
        Rect source = sprite.textureRect;
        float sourceAspect = source.width / Mathf.Max(1f, source.height);
        float destinationAspect = destination.width / Mathf.Max(1f, destination.height);
        Rect target = destination;

        if (sourceAspect > destinationAspect)
        {
            float fittedHeight = destination.width / sourceAspect;
            target.y += (destination.height - fittedHeight) * 0.5f;
            target.height = fittedHeight;
        }
        else
        {
            float fittedWidth = destination.height * sourceAspect;
            target.x += (destination.width - fittedWidth) * 0.5f;
            target.width = fittedWidth;
        }

        Rect uv = new Rect(
            source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height);
        GUI.DrawTextureWithTexCoords(target, texture, uv, true);
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

    private void EnsureStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 30, 24, 36),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            titleStyle.normal.textColor = new Color(1f, 0.87f, 0.66f);
        }

        if (vendorStyle == null)
        {
            vendorStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 38, 20, 30),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            vendorStyle.normal.textColor = new Color(1f, 0.87f, 0.66f);
        }

        if (normalStyle == null)
        {
            normalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 44, 17, 26),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            normalStyle.normal.textColor = new Color(1f, 0.93f, 0.82f);
        }

        if (smallStyle == null)
        {
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(Screen.height / 54, 15, 21),
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