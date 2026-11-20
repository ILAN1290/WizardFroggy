using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    public int Size => slots.Count;
    public List<ItemStack> slots = new List<ItemStack>();
    public event Action OnChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        // InventoryUI lives below the Canvas in the original scene. Move only the
        // inventory data component into its own root object so it survives scene changes.
        if (transform.parent != null)
        {
            List<ItemStack> existingSlots = slots;
            GameObject persistentObject = new GameObject("PersistentInventory");
            Inventory persistent = persistentObject.AddComponent<Inventory>();
            persistent.slots = existingSlots ?? new List<ItemStack>();
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Init(int size)
    {
        if (slots == null) slots = new List<ItemStack>();

        // A scene may rebuild its UI, but must never erase a player's run inventory.
        while (slots.Count < size)
            slots.Add(new ItemStack());
        while (slots.Count > size)
            slots.RemoveAt(slots.Count - 1);

        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == null)
                slots[i] = new ItemStack();

        OnChanged?.Invoke();
    }

    public bool CanAdd(Item item, int count = 1)
    {
        if (item == null || count <= 0) return false;

        int remaining = count;
        foreach (ItemStack stack in slots)
        {
            if (stack == null || stack.IsEmpty)
                remaining -= item.maxStack;
            else if (stack.item == item)
                remaining -= Mathf.Max(0, item.maxStack - stack.count);

            if (remaining <= 0) return true;
        }

        return false;
    }

    public bool Add(Item item, int count = 1)
    {
        if (item == null || count <= 0) return false;

        for (int i = 0; i < slots.Count && count > 0; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty || stack.item != item || stack.count >= item.maxStack)
                continue;

            int canAdd = Mathf.Min(count, item.maxStack - stack.count);
            stack.count += canAdd;
            count -= canAdd;
        }

        for (int i = 0; i < slots.Count && count > 0; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null)
            {
                stack = new ItemStack();
                slots[i] = stack;
            }
            if (!stack.IsEmpty) continue;

            int canAdd = Mathf.Min(count, item.maxStack);
            stack.item = item;
            stack.count = canAdd;
            count -= canAdd;
        }

        OnChanged?.Invoke();
        return count == 0;
    }

    public void RemoveAt(int index, int count = 1)
    {
        if (index < 0 || index >= slots.Count || count <= 0) return;
        if (slots[index] == null || slots[index].IsEmpty) return;

        slots[index].count -= count;
        if (slots[index].count <= 0) slots[index].Clear();
        OnChanged?.Invoke();
    }

    public void Swap(int a, int b)
    {
        if (a < 0 || b < 0 || a >= slots.Count || b >= slots.Count) return;
        ItemStack tmp = slots[a];
        slots[a] = slots[b];
        slots[b] = tmp;
        OnChanged?.Invoke();
    }

    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    public void NotifyChanged()
    {
        OnChanged?.Invoke();
    }
}
