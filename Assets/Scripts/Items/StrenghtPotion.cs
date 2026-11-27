using UnityEngine;

[CreateAssetMenu(fileName = "StrengthPotion", menuName = "Inventory/Potions/Strength Potion")]
public class StrengthPotion : Potion
{
    public float damageMultiplier = 2f;
    public float duration = 15f;
    public GameObject strengthVFX;

    protected override bool ApplyEffect(GameObject user, PlayerEffects effects)
    {
        TowerDefenceManager towerDefence = TowerDefenceManager.Instance;
        if (towerDefence != null && towerDefence.IsGameSessionRunning)
            return towerDefence.TryApplyStrengthPotion(damageMultiplier, duration);

        effects.Strength(damageMultiplier, duration, strengthVFX);
        return true;
    }
}