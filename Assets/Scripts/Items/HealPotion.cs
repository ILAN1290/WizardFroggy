using UnityEngine;

[CreateAssetMenu(fileName = "HealingPotion", menuName = "Inventory/Potions/Healing Potion")]
public class HealingPotion : Potion
{
    public float healAmount = 30f;
    public GameObject healVFX;

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

        if (healVFX != null)
            Instantiate(healVFX, user.transform.position, Quaternion.identity, user.transform);

        return true;
    }
}