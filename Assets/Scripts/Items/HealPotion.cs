// HealingPotion.cs
using UnityEngine;

[CreateAssetMenu(fileName = "HealingPotion", menuName = "Inventory/Potions/Healing Potion")]
public class HealingPotion : Potion
{
    public float healAmount = 30f;

    protected override bool ApplyEffect(GameObject user, PlayerEffects effects)
    {
        PlayerHealth hp = user.GetComponent<PlayerHealth>();
        if (hp == null) return false;

        if (hp.CurrentHealth >= hp.MaxHealth)
        {
            Debug.Log("ХП и так полное, зелье не тратим");
            return false;
        }

        hp.Healing(healAmount);
        return true;
    }
}