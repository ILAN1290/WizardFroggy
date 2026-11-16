using UnityEngine;

public abstract class Potion : Item
{
    [Min(0f)] public float cooldown = 15f;

    public override bool Use(GameObject user)
    {
        PlayerEffects effects = user.GetComponent<PlayerEffects>();
        if (effects == null)
        {
            Debug.LogError($"На {user.name} нет PlayerEffects!");
            return false;
        }

        if (effects.IsOnCooldown(this))
        {
            Debug.Log($"{itemName}: кулдаун ещё {effects.GetCooldownLeft(this):F1} сек");
            return false;
        }

        if (!ApplyEffect(user, effects))
            return false;

        effects.StartCooldown(this, cooldown);
        return true;
    }

    protected abstract bool ApplyEffect(GameObject user, PlayerEffects effects);
}