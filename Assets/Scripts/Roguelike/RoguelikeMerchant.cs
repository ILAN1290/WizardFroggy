using UnityEngine;

public class RoguelikeMerchant : MonoBehaviour
{
    private RoguelikeSession session;

    public void Initialize(RoguelikeSession owner)
    {
        session = owner;
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
        Inventory inventory = Inventory.Instance;

        Rect panel = new Rect(Screen.width * 0.5f - 330f,
            Screen.height * 0.5f - 285f, 660f, 570f);
        GUI.Box(panel, "ЛАВКА СТАРОГО БИБЛИОТЕКАРЯ");
        GUI.Label(new Rect(panel.x + 24, panel.y + 34, 600, 28),
            "Грибочки: " + data.Mushrooms);

        GUI.Label(new Rect(panel.x + 24, panel.y + 72, 290, 28), "Предметы");
        GUI.Label(new Rect(panel.x + 350, panel.y + 72, 285, 28),
            "Улучшения посоха и персонажа");

        Item[] items = Resources.LoadAll<Item>("RoguelikeLoot");
        float itemY = panel.y + 104f;
        for (int i = 0; i < items.Length && i < 6; i++)
        {
            Item item = items[i];
            int price = GetItemPrice(item);
            if (GUI.Button(new Rect(panel.x + 24, itemY, 290, 34),
                item.itemName + " • " + price + " гриб."))
                BuyItem(item, price);
            itemY += 38f;
        }

        DrawUpgradeButton(data, panel, RoguelikeUpgradeType.StaffDamage,
            "Урон посоха", panel.y + 104f);
        DrawUpgradeButton(data, panel, RoguelikeUpgradeType.ManaConsumption,
            "Расход маны", panel.y + 148f);
        DrawUpgradeButton(data, panel, RoguelikeUpgradeType.ManaRegeneration,
            "Восстановление маны", panel.y + 192f);
        DrawUpgradeButton(data, panel, RoguelikeUpgradeType.MaximumHealth,
            "Максимальное здоровье", panel.y + 236f);
        DrawUpgradeButton(data, panel, RoguelikeUpgradeType.Defense,
            "Защита", panel.y + 280f);

        GUI.Label(new Rect(panel.x + 24, panel.y + 350f, 600, 26),
            "Продажа предметов из инвентаря");
        float sellY = panel.y + 380f;

        if (inventory != null)
        {
            int shown = 0;
            for (int i = 0; i < inventory.Size && shown < 4; i++)
            {
                ItemStack stack = inventory.GetSlot(i);
                if (stack == null || stack.IsEmpty) continue;

                int sellPrice = Mathf.Max(1, GetItemPrice(stack.item) / 2);
                if (GUI.Button(new Rect(panel.x + 24, sellY, 400, 28),
                    "Продать 1 × " + stack.item.itemName + "  (+" + sellPrice + ")"))
                    SellOne(i, sellPrice);

                sellY += 31f;
                shown++;
            }
        }

        if (GUI.Button(new Rect(panel.x + 475, panel.y + 520f, 160, 32),
            "Закрыть"))
            owner.CloseShop();
    }

    private void DrawUpgradeButton(
        PlayerRunData data, Rect panel, RoguelikeUpgradeType type,
        string title, float y)
    {
        int level = data.GetUpgradeLevel(type);
        bool maxed = !data.CanUpgrade(type);
        int price = data.GetUpgradePrice(type);

        string label = maxed
            ? title + " • ур. " + level + " • МАКС."
            : title + " " + GetUpgradeValue(data, type, false) +
              " → " + GetUpgradeValue(data, type, true) +
              " • " + price + " гриб.";

        bool previousGuiEnabled = GUI.enabled;
        GUI.enabled = previousGuiEnabled && !maxed && data.Mushrooms >= price;
        if (GUI.Button(new Rect(panel.x + 350, y, 285, 38), label))
            BuyUpgrade(type);
        GUI.enabled = previousGuiEnabled;
    }

    private static string GetUpgradeValue(
        PlayerRunData data, RoguelikeUpgradeType type, bool next)
    {
        int level = data.GetUpgradeLevel(type) + (next ? 1 : 0);
        switch (type)
        {
            case RoguelikeUpgradeType.StaffDamage:
                return (7.5f + level * 2.5f).ToString("0.0");
            case RoguelikeUpgradeType.ManaConsumption:
                return Mathf.Max(5f, 20f - level * 1.5f).ToString("0.0");
            case RoguelikeUpgradeType.ManaRegeneration:
                return (15f + level * 2.5f).ToString("0.0") + "/с";
            case RoguelikeUpgradeType.MaximumHealth:
                return (100f + level * 20f).ToString("0");
            case RoguelikeUpgradeType.Defense:
                return (level * 4).ToString("0") + "%";
            default:
                return "—";
        }
    }

    private void BuyUpgrade(RoguelikeUpgradeType type)
    {
        PlayerRunData data = PlayerRunData.EnsureInstance();
        int price = data.GetUpgradePrice(type);
        if (!data.CanUpgrade(type))
        {
            RoguelikeSession.Instance.ShowHint("Это улучшение уже максимального уровня.");
            return;
        }
        if (!data.TryUpgrade(type))
        {
            RoguelikeSession.Instance.ShowHint("Не хватает грибочков.");
            return;
        }

        if (type == RoguelikeUpgradeType.MaximumHealth)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            PlayerHealth playerHealth = player != null
                ? player.GetComponent<PlayerHealth>() : null;
            if (playerHealth != null)
                playerHealth.ApplyMaximumHealthUpgrade();
        }

        string upgradeName = type == RoguelikeUpgradeType.StaffDamage
            ? "Урон посоха"
            : type == RoguelikeUpgradeType.ManaConsumption
                ? "Расход маны"
                : type == RoguelikeUpgradeType.ManaRegeneration
                    ? "Восстановление маны"
                    : type == RoguelikeUpgradeType.MaximumHealth
                        ? "Максимальное здоровье" : "Защита";
        RoguelikeSession.Instance.ShowHint(
            upgradeName + " улучшено до " + data.GetUpgradeLevel(type) +
            " уровня. Потрачено " + price + " грибочков.");
    }

    private void BuyItem(Item item, int price)
    {
        if (item == null) return;
        PlayerRunData data = PlayerRunData.EnsureInstance();

        if (Inventory.Instance == null || !Inventory.Instance.CanAdd(item, 1))
        {
            RoguelikeSession.Instance.ShowHint("Нет свободного места в инвентаре.");
            return;
        }

        if (!data.SpendMushrooms(price))
        {
            RoguelikeSession.Instance.ShowHint("Не хватает грибочков.");
            return;
        }

        Inventory.Instance.Add(item, 1);
        RoguelikeSession.Instance.ShowHint("Куплено: " + item.itemName);
    }

    private void SellOne(int slotIndex, int price)
    {
        if (Inventory.Instance == null) return;
        ItemStack stack = Inventory.Instance.GetSlot(slotIndex);
        if (stack == null || stack.IsEmpty) return;

        Inventory.Instance.RemoveAt(slotIndex, 1);
        PlayerRunData.EnsureInstance().AddMushrooms(price);
        RoguelikeSession.Instance.ShowHint(
            "Предмет продан. Получено грибочков: " + price);
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

}
