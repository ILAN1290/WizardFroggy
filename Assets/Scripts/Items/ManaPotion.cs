// ManaPotion.cs
using UnityEngine;

[CreateAssetMenu(fileName = "ManaPotion", menuName = "Inventory/Potions/Mana Potion")]
public class ManaPotion : Potion
{
    public float manaAmount = 30f;

    protected override bool ApplyEffect(GameObject user, PlayerEffects effects)
    {
        PlayerMana mana = user.GetComponent<PlayerMana>();
        if (mana == null) return false;

        if (mana.CurrentMana >= mana.MaxMana)
        {
            Debug.Log("Мана и так полная, зелье не тратим");
            return false;
        }

        mana.RestoreMana(manaAmount);
        return true;
    }
}