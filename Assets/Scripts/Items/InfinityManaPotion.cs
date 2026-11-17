using UnityEngine;

[CreateAssetMenu(fileName = "InfiniteManaPotion", menuName = "Inventory/Potions/Infinite Mana Potion")]
public class InfiniteManaPotion : Potion
{
    public float duration = 10f;
    public GameObject infiniteManaVFX;

    protected override bool ApplyEffect(GameObject user, PlayerEffects effects)
    {
        effects.InfiniteMana(duration, infiniteManaVFX);
        return true;
    }
}