using UnityEngine;

[CreateAssetMenu(fileName = "SpeedPotion", menuName = "Inventory/Potions/Speed Potion")]
public class SpeedPotion : Potion
{
    public float speedMultiplier = 1.5f;
    public float duration = 10f;
    public GameObject speedVFX;

    protected override bool ApplyEffect(GameObject user, PlayerEffects effects)
    {
        // Keep PlayerEffects in sync for UI/other systems, and apply the boost
        // directly to the movement controller so physics consumes it reliably.
        effects.Speed(speedMultiplier, duration, speedVFX);
        PlayerController controller = user.GetComponent<PlayerController>();
        if (controller != null)
            controller.ApplySpeedPotion(speedMultiplier, duration);
        return true;
    }
}