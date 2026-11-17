using UnityEngine;

/// <summary>
/// Persistent player data shared by scenes. Scene objects (player and UI) may be recreated,
/// but currency, equipped staff, health, mana and the Inventory singleton are retained.
/// </summary>
public class PlayerRunData : MonoBehaviour
{
    private const string MushroomsKey = "WizardFroggy.Roguelike.Mushrooms";
    private const string StaffKey = "WizardFroggy.Roguelike.Staff";
    private const string HealthKey = "WizardFroggy.Roguelike.Health";
    private const string ManaKey = "WizardFroggy.Roguelike.Mana";

    public static PlayerRunData Instance { get; private set; }

    public int Mushrooms { get; private set; }
    public StaffData EquippedStaff { get; private set; }

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
        savedHealth = PlayerPrefs.HasKey(HealthKey) ? PlayerPrefs.GetFloat(HealthKey) : -1f;
        savedMana = PlayerPrefs.HasKey(ManaKey) ? PlayerPrefs.GetFloat(ManaKey) : -1f;

        string staffId = PlayerPrefs.GetString(StaffKey, "willow");
        StaffData[] available = Resources.LoadAll<StaffData>("RoguelikeStaves");
        foreach (StaffData staff in available)
        {
            if (staff != null && staff.staffId == staffId)
            {
                EquippedStaff = staff;
                break;
            }
        }

        if (EquippedStaff == null && available.Length > 0)
            EquippedStaff = available[0];
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

    public void EquipStaff(StaffData staff)
    {
        if (staff == null) return;
        EquippedStaff = staff;
        PlayerPrefs.SetString(StaffKey, staff.staffId);
        PlayerPrefs.Save();
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
