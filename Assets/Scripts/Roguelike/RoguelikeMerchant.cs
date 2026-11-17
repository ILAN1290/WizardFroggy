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

        Rect panel = new Rect(Screen.width * 0.5f - 300f,
            Screen.height * 0.5f - 235f, 600f, 470f);
        GUI.Box(panel, "ЛАВКА СТАРОГО БИБЛИОТЕКАРЯ");
        GUI.Label(new Rect(panel.x + 24, panel.y + 34, 540, 28),
            "Грибочки: " + data.Mushrooms);

        Item[] items = Resources.LoadAll<Item>("RoguelikeLoot");
        float y = panel.y + 72f;
        GUI.Label(new Rect(panel.x + 24, y, 260, 28), "Предметы");
        GUI.Label(new Rect(panel.x + 315, y, 260, 28), "Посохи");
        y += 32f;

        for (int i = 0; i < items.Length && i < 6; i++)
        {
            Item item = items[i];
            int price = GetItemPrice(item);
            if (GUI.Button(new Rect(panel.x + 24, y, 270, 34),
                item.itemName + " • " + price + " гриб."))
                BuyItem(item, price);
            y += 38f;
        }

        StaffData[] staves = Resources.LoadAll<StaffData>("RoguelikeStaves");
        float staffY = panel.y + 104f;
        for (int i = 0; i < staves.Length && i < 6; i++)
        {
            StaffData staff = staves[i];
            int price = GetStaffPrice(staff);
            string equipped = data.EquippedStaff == staff ? " ✓" : "";
            string label = staff.displayName + equipped + " | ур." +
                staff.damage.ToString("0") + " | " +
                staff.shotsPerSecond.ToString("0.0") + "/с | мана " +
                staff.manaCost.ToString("0");
            if (GUI.Button(new Rect(panel.x + 315, staffY, 260, 44),
                label + " • " + price))
                BuyStaff(staff, price);
            staffY += 48f;
        }

        float sellY = panel.y + 310f;
        GUI.Label(new Rect(panel.x + 24, sellY, 540, 26),
            "Продажа предметов из инвентаря");
        sellY += 28f;

        if (inventory != null)
        {
            int shown = 0;
            for (int i = 0; i < inventory.Size && shown < 6; i++)
            {
                ItemStack stack = inventory.GetSlot(i);
                if (stack == null || stack.IsEmpty) continue;

                int sellPrice = Mathf.Max(1, GetItemPrice(stack.item) / 2);
                if (GUI.Button(new Rect(panel.x + 24, sellY, 330, 30),
                    "Продать 1 × " + stack.item.itemName + "  (+" + sellPrice + ")"))
                    SellOne(i, sellPrice);

                sellY += 32f;
                shown++;
                if (sellY > panel.y + 420f) break;
            }
        }

        if (GUI.Button(new Rect(panel.x + 390, panel.y + 420, 185, 34),
            "Закрыть"))
            owner.CloseShop();
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

    private void BuyStaff(StaffData staff, int price)
    {
        if (staff == null) return;
        PlayerRunData data = PlayerRunData.EnsureInstance();

        if (data.EquippedStaff == staff)
        {
            RoguelikeSession.Instance.ShowHint("Этот посох уже экипирован.");
            return;
        }
        if (!data.SpendMushrooms(price))
        {
            RoguelikeSession.Instance.ShowHint("Не хватает грибочков.");
            return;
        }

        data.EquipStaff(staff);
        RoguelikeSession.Instance.ShowHint("Экипирован посох: " + staff.displayName);
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

    private int GetStaffPrice(StaffData staff)
    {
        if (staff == null) return 0;
        if (staff.staffId == "oak") return 55;
        if (staff.staffId == "crystal") return 90;
        return 0;
    }
}
