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
/// Persistent roguelike progress. The player keeps one basic staff and upgrades
/// its properties instead of buying and equipping different staffs.
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

    public static PlayerRunData Instance { get; private set; }

    public int Mushrooms { get; private set; }
    public int HighestUnlockedFloor { get; private set; } = 1;
    public bool RoguelikeCompleted { get; private set; }

    public int StaffDamageLevel { get; private set; }
    public int ManaConsumptionLevel { get; private set; }
    public int ManaRegenerationLevel { get; private set; }
    public int MaximumHealthLevel { get; private set; }
    public int DefenseLevel { get; private set; }

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
        ManaConsumptionLevel = Mathf.Clamp(PlayerPrefs.GetInt(ManaConsumptionLevelKey, 0), 0, 10);
        ManaRegenerationLevel = Mathf.Clamp(PlayerPrefs.GetInt(ManaRegenerationLevelKey, 0), 0, 10);
        MaximumHealthLevel = Mathf.Clamp(PlayerPrefs.GetInt(MaximumHealthLevelKey, 0), 0, 10);
        DefenseLevel = Mathf.Clamp(PlayerPrefs.GetInt(DefenseLevelKey, 0), 0, 10);
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
            PlayerPrefs.SetInt(HighestFloorKey, HighestUnlockedFloor);
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
        int level = GetUpgradeLevel(type);
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

    public bool TryUpgrade(RoguelikeUpgradeType type)
    {
        if (!CanUpgrade(type)) return false;
        if (!SpendMushrooms(GetUpgradePrice(type))) return false;

        switch (type)
        {
            case RoguelikeUpgradeType.StaffDamage:
                StaffDamageLevel++;
                PlayerPrefs.SetInt(StaffDamageLevelKey, StaffDamageLevel);
                break;
            case RoguelikeUpgradeType.ManaConsumption:
                ManaConsumptionLevel++;
                PlayerPrefs.SetInt(ManaConsumptionLevelKey, ManaConsumptionLevel);
                break;
            case RoguelikeUpgradeType.ManaRegeneration:
                ManaRegenerationLevel++;
                PlayerPrefs.SetInt(ManaRegenerationLevelKey, ManaRegenerationLevel);
                break;
            case RoguelikeUpgradeType.MaximumHealth:
                MaximumHealthLevel++;
                PlayerPrefs.SetInt(MaximumHealthLevelKey, MaximumHealthLevel);
                break;
            case RoguelikeUpgradeType.Defense:
                DefenseLevel++;
                PlayerPrefs.SetInt(DefenseLevelKey, DefenseLevel);
                break;
        }

        PlayerPrefs.Save();
        return true;
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