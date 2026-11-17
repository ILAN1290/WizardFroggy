using UnityEngine;

[CreateAssetMenu(fileName = "NewStaff", menuName = "Roguelike/Staff")]
public class StaffData : ScriptableObject
{
    public string staffId = "willow";
    public string displayName = "Ивовый посох";
    [Min(0.1f)] public float damage = 8f;
    [Min(0.1f)] public float shotsPerSecond = 2.5f;
    [Min(0f)] public float manaCost = 20f;

    public float ShotCooldown => 1f / Mathf.Max(0.1f, shotsPerSecond);
}
