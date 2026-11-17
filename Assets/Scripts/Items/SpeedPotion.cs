using UnityEngine;

[CreateAssetMenu(fileName = "SpeedPotion", menuName = "Inventory/Potions/Speed Potion")]
public class SpeedPotion : Potion
{
    public float speedMultiplier = 1.5f;
    public float duration = 10f;
    public GameObject speedVFX;

    protected override bool ApplyEffect(GameObject user, PlayerEffects effects)
    {
        effects.Speed(speedMultiplier, duration, speedVFX);
        return true;
    }
}