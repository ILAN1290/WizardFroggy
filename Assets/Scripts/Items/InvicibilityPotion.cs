using UnityEngine;

[CreateAssetMenu(fileName = "InvincibilityPotion", menuName = "Inventory/Potions/Invincibility Potion")]
public class InvincibilityPotion : Potion
{
    public float duration = 10f;
    public GameObject invincibilityVFX;

    protected override bool ApplyEffect(GameObject user, PlayerEffects effects)
    {
        effects.Invincibility(duration, invincibilityVFX);
        return true;
    }
}