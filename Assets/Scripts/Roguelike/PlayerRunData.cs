using UnityEngine;

public enum RoguelikeUpgradeType
{
    StaffDamage,
    ManaConsumption,
    ManaRegeneration,
    MaximumHealth,
    Defense
}

/// <summary>
/// Persistent roguelike progress with five independent upgrade tracks.
/// </summary>
public class PlayerRunData : MonoBehaviour
{
    private const string MushroomsKey = "WizardFroggy.Roguelike.Mushrooms";
    private const string HealthKey = "WizardFroggy.Roguelike.Health";
    private const string ManaKey = "WizardFroggy.Roguelike.Mana";
    private const string HighestFloorKey = "WizardFroggy.Roguelike.HighestFloor";
    private const string CompletedKey = "WizardFroggy.Roguelike.Completed";

    private const string StaffDamageLevelKey = "WizardFroggy.Roguelike.Upgrade.StaffDamage";
    private const string ManaConsumptionLevelKey = "WizardFroggy.Roguelike.Upgrade.ManaConsumption";
    private const string ManaRegenerationLevelKey = "WizardFroggy.Roguelike.Upgrade.ManaRegeneration";
    private const string MaximumHealthLevelKey = "WizardFroggy.Roguelike.Upgrade.MaximumHealth";
    private const string DefenseLevelKey = "WizardFroggy.Roguelike.Upgrade.Defense";

    // Keys from the temporary three-track version of the merchant.
    private const string CombinedManaLevelKey = "WizardFroggy.Roguelike.Upgrade.Mana";
    private const string CombinedVitalityLevelKey = "WizardFroggy.Roguelike.Upgrade.Vitality";

    public static PlayerRunData Instance { get; private set; }

    public int Mushrooms { get; private set; }
    public int HighestUnlockedFloor { get; private set; } = 1;
    public bool RoguelikeCompleted { get; private set; }

    public int StaffDamageLevel { get; private set; }
    public int ManaConsumptionLevel { get; private set; }
    public int ManaRegenerationLevel { get; private set; }
    public int MaximumHealthLevel { get; private set; }
    public int DefenseLevel { get; private set; }

    // Backward-compatible combined levels for any older external scripts.
    public int ManaLevel => Mathf.Max(ManaConsumptionLevel, ManaRegenerationLevel);
    public int VitalityLevel => Mathf.Max(MaximumHealthLevel, DefenseLevel);

    public float StaffDamage => 7.5f + StaffDamageLevel * 2.5f;
    public float ProjectileManaCost => Mathf.Max(5f, 20f - ManaConsumptionLevel * 1.5f);
    public float ManaRegeneration => 15f + ManaRegenerationLevel * 2.5f;
    public float MaximumHealth => 100f + MaximumHealthLevel * 20f;
    public float DamageReduction => Mathf.Clamp01(DefenseLevel * 0.04f);

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

        // Restore the two original independent values when they exist. If a save
        // was created by the short-lived three-track version, seed both from its
        // combined level so no purchased progress disappears.
        int combinedMana = PlayerPrefs.GetInt(CombinedManaLevelKey, 0);
        int combinedVitality = PlayerPrefs.GetInt(CombinedVitalityLevelKey, 0);
        ManaConsumptionLevel = Mathf.Clamp(
            PlayerPrefs.GetInt(ManaConsumptionLevelKey, combinedMana), 0, 10);
        ManaRegenerationLevel = Mathf.Clamp(
            PlayerPrefs.GetInt(ManaRegenerationLevelKey, combinedMana), 0, 10);
        MaximumHealthLevel = Mathf.Clamp(
            PlayerPrefs.GetInt(MaximumHealthLevelKey, combinedVitality), 0, 10);
        DefenseLevel = Mathf.Clamp(
            PlayerPrefs.GetInt(DefenseLevelKey, combinedVitality), 0, 10);

        SaveUpgradeLevels();
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
            case RoguelikeUpgradeType.ManaConsumption: return ManaConsumptionLevel;
            case RoguelikeUpgradeType.ManaRegeneration: return ManaRegenerationLevel;
            case RoguelikeUpgradeType.MaximumHealth: return MaximumHealthLevel;
            case RoguelikeUpgradeType.Defense: return DefenseLevel;
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
        return Mathf.FloorToInt(GetUpgradePriceAtLevel(type, level - 1) * 0.5f);
    }

    private static int GetUpgradePriceAtLevel(RoguelikeUpgradeType type, int level)
    {
        switch (type)
        {
            case RoguelikeUpgradeType.StaffDamage: return 35 + level * 25;
            case RoguelikeUpgradeType.ManaConsumption: return 30 + level * 22;
            case RoguelikeUpgradeType.ManaRegeneration: return 30 + level * 22;
            case RoguelikeUpgradeType.MaximumHealth: return 40 + level * 25;
            case RoguelikeUpgradeType.Defense: return 50 + level * 30;
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
        if (!CanUpgrade(type) || !SpendMushrooms(GetUpgradePrice(type)))
            return false;

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
            case RoguelikeUpgradeType.ManaConsumption:
                ManaConsumptionLevel = level;
                PlayerPrefs.SetInt(ManaConsumptionLevelKey, level);
                break;
            case RoguelikeUpgradeType.ManaRegeneration:
                ManaRegenerationLevel = level;
                PlayerPrefs.SetInt(ManaRegenerationLevelKey, level);
                break;
            case RoguelikeUpgradeType.MaximumHealth:
                MaximumHealthLevel = level;
                PlayerPrefs.SetInt(MaximumHealthLevelKey, level);
                break;
            case RoguelikeUpgradeType.Defense:
                DefenseLevel = level;
                PlayerPrefs.SetInt(DefenseLevelKey, level);
                break;
        }

        SaveUpgradeLevels();
    }

    private void SaveUpgradeLevels()
    {
        PlayerPrefs.SetInt(StaffDamageLevelKey, StaffDamageLevel);
        PlayerPrefs.SetInt(ManaConsumptionLevelKey, ManaConsumptionLevel);
        PlayerPrefs.SetInt(ManaRegenerationLevelKey, ManaRegenerationLevel);
        PlayerPrefs.SetInt(MaximumHealthLevelKey, MaximumHealthLevel);
        PlayerPrefs.SetInt(DefenseLevelKey, DefenseLevel);
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