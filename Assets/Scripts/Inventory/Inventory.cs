using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour {
    public static Inventory Instance { get; private set; }

    public int Size => slots.Count;
    public List<ItemStack> slots = new List<ItemStack>();
    
    public event Action OnChanged;

    private void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Init(int size) {
        slots.Clear();
        for (int i = 0; i < size; i++)
            slots.Add(new ItemStack());
        OnChanged?.Invoke();
    }

    // Пытается добавить предмет. Возвращает true если всё влезло.
    public bool Add(Item item, int count = 1) {
        // Сначала ищем слоты где такой же предмет и есть место
        for (int i = 0; i < slots.Count; i++) {
            if (!slots[i].IsEmpty && slots[i].item == item && slots[i].count < item.maxStack) {
                int canAdd = Mathf.Min(count, item.maxStack - slots[i].count);
                slots[i].count += canAdd;
                count -= canAdd;
                if (count <= 0) { OnChanged?.Invoke(); return true; }
            }
        }
        // Потом ищем пустые слоты
        for (int i = 0; i < slots.Count; i++) {
            if (slots[i].IsEmpty) {
                int canAdd = Mathf.Min(count, item.maxStack);
                slots[i].item = item;
                slots[i].count = canAdd;
                count -= canAdd;
                if (count <= 0) { OnChanged?.Invoke(); return true; }
            }
        }
        OnChanged?.Invoke();
        return count <= 0;
    }

    public void RemoveAt(int index, int count = 1) {
        if (index < 0 || index >= slots.Count) return;
        slots[index].count -= count;
        if (slots[index].count <= 0) slots[index].Clear();
        OnChanged?.Invoke();
    }

    public void Swap(int a, int b) {
        if (a < 0 || b < 0 || a >= slots.Count || b >= slots.Count) return;
        var tmp = slots[a];
        slots[a] = slots[b];
        slots[b] = tmp;
        OnChanged?.Invoke();
    }

    public ItemStack GetSlot(int index) => slots[index];

    public void NotifyChanged() {
        OnChanged?.Invoke();
    }
}