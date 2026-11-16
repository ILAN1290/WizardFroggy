// SpeedPotion.cs
using UnityEngine;

[CreateAssetMenu(fileName = "SpeedPotion", menuName = "Inventory/Potions/Speed Potion")]
public class SpeedPotion : Potion
{
    public float speedMultiplier = 1.5f;
    public float duration = 15f;

    protected override bool ApplyEffect(GameObject user, PlayerEffects effects)
    {
        effects.Speed(speedMultiplier, duration);
        return true;
    }
}