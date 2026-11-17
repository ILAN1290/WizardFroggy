using UnityEngine;

public abstract class Potion : Item
{
    [Min(0f)] public float cooldown = 15f;

    public AudioClip drinkSound;
    public AudioClip effectSound;

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

        if (drinkSound != null)
            AudioSource.PlayClipAtPoint(drinkSound, user.transform.position);

        if (!ApplyEffect(user, effects))
            return false;

        if (effectSound != null)
            AudioSource.PlayClipAtPoint(effectSound, user.transform.position);

        effects.StartCooldown(this, cooldown);
        return true;
    }

    protected abstract bool ApplyEffect(GameObject user, PlayerEffects effects);
}