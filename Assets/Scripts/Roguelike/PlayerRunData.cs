using UnityEngine;

public enum RoguelikeUpgradeType
{
    StaffDamage,
    Mana,
    Vitality
}

/// <summary>
/// Persistent roguelike progress and the three upgrade tracks shown by the merchant.
/// The Mana track improves both regeneration and projectile efficiency.
/// The Vitality track improves maximum health and damage resistance.
/// </summary>
public class PlayerRunData : MonoBehaviour
{
    private const string MushroomsKey = "WizardFroggy.Roguelike.Mushrooms";
    private const string HealthKey = "WizardFroggy.Roguelike.Health";
    private const string ManaKey = "WizardFroggy.Roguelike.Mana";
    private const string HighestFloorKey = "WizardFroggy.Roguelike.HighestFloor";
    private const string CompletedKey = "WizardFroggy.Roguelike.Completed";

    private const string StaffDamageLevelKey = "WizardFroggy.Roguelike.Upgrade.StaffDamage";
    private const string ManaLevelKey = "WizardFroggy.Roguelike.Upgrade.Mana";
    private const string VitalityLevelKey = "WizardFroggy.Roguelike.Upgrade.Vitality";

    // Previous versions stored mana and survivability in separate tracks.
    // Keep their progress when migrating existing saves to the three-track shop.
    private const string LegacyManaConsumptionLevelKey = "WizardFroggy.Roguelike.Upgrade.ManaConsumption";
    private const string LegacyManaRegenerationLevelKey = "WizardFroggy.Roguelike.Upgrade.ManaRegeneration";
    private const string LegacyMaximumHealthLevelKey = "WizardFroggy.Roguelike.Upgrade.MaximumHealth";
    private const string LegacyDefenseLevelKey = "WizardFroggy.Roguelike.Upgrade.Defense";

    public static PlayerRunData Instance { get; private set; }

    public int Mushrooms { get; private set; }
    public int HighestUnlockedFloor { get; private set; } = 1;
    public bool RoguelikeCompleted { get; private set; }

    public int StaffDamageLevel { get; private set; }
    public int ManaLevel { get; private set; }
    public int VitalityLevel { get; private set; }

    // Compatibility properties for other gameplay systems and older integrations.
    public int ManaConsumptionLevel => ManaLevel;
    public int ManaRegenerationLevel => ManaLevel;
    public int MaximumHealthLevel => VitalityLevel;
    public int DefenseLevel => VitalityLevel;

    public float StaffDamage => 7.5f + StaffDamageLevel * 2.5f;
    public float ProjectileManaCost => Mathf.Max(5f, 20f - ManaLevel * 1.5f);
    public float ManaRegeneration => 15f + ManaLevel * 2.5f;
    public float MaximumHealth => 100f + VitalityLevel * 20f;
    public float DamageReduction => Mathf.Clamp01(VitalityLevel * 0.04f);

    public int CurrentProgressFloor => Mathf.Clamp(HighestUnlockedFloor, 1, 3);

    private float savedHealth = -1f;
    private float savedMana = -1f;

    public static PlayerRunData EnsureInstance()
    {
        if (Instance != null) return Instance;
        GameObject dataObject = new GameObject("PlayerRunData");
        return dataObject.AddComponent<PlayerRunData>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    private void Load()
    {
        Mushrooms = PlayerPrefs.GetInt(MushroomsKey, 0);
        HighestUnlockedFloor = Mathf.Clamp(PlayerPrefs.GetInt(HighestFloorKey, 1), 1, 4);
        RoguelikeCompleted = PlayerPrefs.GetInt(CompletedKey, 0) == 1;
        savedHealth = PlayerPrefs.HasKey(HealthKey) ? PlayerPrefs.GetFloat(HealthKey) : -1f;
        savedMana = PlayerPrefs.HasKey(ManaKey) ? PlayerPrefs.GetFloat(ManaKey) : -1f;

        StaffDamageLevel = Mathf.Clamp(PlayerPrefs.GetInt(StaffDamageLevelKey, 0), 0, 10);

        int legacyManaLevel = Mathf.Max(
            PlayerPrefs.GetInt(LegacyManaConsumptionLevelKey, 0),
            PlayerPrefs.GetInt(LegacyManaRegenerationLevelKey, 0));
        ManaLevel = Mathf.Clamp(PlayerPrefs.GetInt(ManaLevelKey, legacyManaLevel), 0, 10);

        int legacyVitalityLevel = Mathf.Max(
            PlayerPrefs.GetInt(LegacyMaximumHealthLevelKey, 0),
            PlayerPrefs.GetInt(LegacyDefenseLevelKey, 0));
        VitalityLevel = Mathf.Clamp(
            PlayerPrefs.GetInt(VitalityLevelKey, legacyVitalityLevel), 0, 10);

        PlayerPrefs.SetInt(ManaLevelKey, ManaLevel);
        PlayerPrefs.SetInt(VitalityLevelKey, VitalityLevel);
    }

    public bool CanStartFloor(int floorNumber)
    {
        return !RoguelikeCompleted && floorNumber == CurrentProgressFloor;
    }

    public void CompleteFloor(int floorNumber)
    {
        if (floorNumber < HighestUnlockedFloor) return;

        if (floorNumber >= 3)
        {
            RoguelikeCompleted = true;
            HighestUnlockedFloor = 4;
            PlayerPrefs.SetInt(CompletedKey, 1);
        }
        else
        {
            HighestUnlockedFloor = Mathf.Max(HighestUnlockedFloor, floorNumber + 1);
        }

        PlayerPrefs.SetInt(HighestFloorKey, HighestUnlockedFloor);
        PlayerPrefs.Save();
    }

    public void AddMushrooms(int amount)
    {
        if (amount <= 0) return;
        Mushrooms += amount;
        PlayerPrefs.SetInt(MushroomsKey, Mushrooms);
        PlayerPrefs.Save();
    }

    public bool SpendMushrooms(int amount)
    {
        if (amount < 0 || Mushrooms < amount) return false;
        Mushrooms -= amount;
        PlayerPrefs.SetInt(MushroomsKey, Mushrooms);
        PlayerPrefs.Save();
        return true;
    }

    public int GetUpgradeLevel(RoguelikeUpgradeType type)
    {
        switch (type)
        {
            case RoguelikeUpgradeType.StaffDamage: return StaffDamageLevel;
            case RoguelikeUpgradeType.Mana: return ManaLevel;
            case RoguelikeUpgradeType.Vitality: return VitalityLevel;
            default: return 0;
        }
    }

    public int GetUpgradePrice(RoguelikeUpgradeType type)
    {
        return GetUpgradePriceAtLevel(type, GetUpgradeLevel(type));
    }

    public int GetUpgradeRefund(RoguelikeUpgradeType type)
    {
        int level = GetUpgradeLevel(type);
        if (level <= 0) return 0;
        int pricePaidForLastLevel = GetUpgradePriceAtLevel(type, level - 1);
        return Mathf.FloorToInt(pricePaidForLastLevel * 0.5f);
    }

    private static int GetUpgradePriceAtLevel(RoguelikeUpgradeType type, int level)
    {
        switch (type)
        {
            case RoguelikeUpgradeType.StaffDamage: return 35 + level * 25;
            case RoguelikeUpgradeType.Mana: return 30 + level * 22;
            case RoguelikeUpgradeType.Vitality: return 50 + level * 30;
            default: return int.MaxValue;
        }
    }

    public bool CanUpgrade(RoguelikeUpgradeType type)
    {
        return GetUpgradeLevel(type) < 10;
    }

    public bool CanDowngrade(RoguelikeUpgradeType type)
    {
        return GetUpgradeLevel(type) > 0;
    }

    public bool TryUpgrade(RoguelikeUpgradeType type)
    {
        if (!CanUpgrade(type)) return false;
        if (!SpendMushrooms(GetUpgradePrice(type))) return false;

        SetUpgradeLevel(type, GetUpgradeLevel(type) + 1);
        PlayerPrefs.Save();
        return true;
    }

    public bool TryDowngrade(RoguelikeUpgradeType type)
    {
        if (!CanDowngrade(type)) return false;

        int refund = GetUpgradeRefund(type);
        SetUpgradeLevel(type, GetUpgradeLevel(type) - 1);
        AddMushrooms(refund);
        PlayerPrefs.Save();
        return true;
    }

    private void SetUpgradeLevel(RoguelikeUpgradeType type, int level)
    {
        level = Mathf.Clamp(level, 0, 10);
        switch (type)
        {
            case RoguelikeUpgradeType.StaffDamage:
                StaffDamageLevel = level;
                PlayerPrefs.SetInt(StaffDamageLevelKey, level);
                break;

            case RoguelikeUpgradeType.Mana:
                ManaLevel = level;
                PlayerPrefs.SetInt(ManaLevelKey, level);
                break;

            case RoguelikeUpgradeType.Vitality:
                VitalityLevel = level;
                PlayerPrefs.SetInt(VitalityLevelKey, level);
                break;
        }
    }

    public float LoadHealth(float defaultValue)
    {
        return savedHealth < 0f ? defaultValue : Mathf.Clamp(savedHealth, 0f, defaultValue);
    }

    public float LoadMana(float defaultValue)
    {
        return savedMana < 0f ? defaultValue : Mathf.Clamp(savedMana, 0f, defaultValue);
    }

    public void SaveHealth(float value)
    {
        savedHealth = Mathf.Max(0f, value);
        PlayerPrefs.SetFloat(HealthKey, savedHealth);
    }

    public void SaveMana(float value)
    {
        savedMana = Mathf.Max(0f, value);
        PlayerPrefs.SetFloat(ManaKey, savedMana);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}