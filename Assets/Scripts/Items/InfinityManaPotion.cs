// InfiniteManaPotion.cs
using UnityEngine;

[CreateAssetMenu(fileName = "InfiniteManaPotion", menuName = "Inventory/Potions/Infinite Mana Potion")]
public class InfiniteManaPotion : Potion
{
    public float duration = 10f;

    protected override bool ApplyEffect(GameObject user, PlayerEffects effects)
    {
        effects.InfiniteMana(duration);
        return true;
    }
}