using UnityEngine;

public class PlayerMana : MonoBehaviour
{
    [SerializeField] private float maxManaValue = 100f;
    [SerializeField] private float fillingSpeed = 15f;
    [HideInInspector] public bool isSpending = false;

    private float currentManaValue;
    private PlayerEffects effects;

    public float CurrentMana { get { return currentManaValue; } }
    public float MaxMana { get { return maxManaValue; } }

    private void Start()
    {
        effects = GetComponent<PlayerEffects>();
        currentManaValue = maxManaValue;
    }

    public void SpendMana(float value)
    {
        if (effects != null && effects.IsInfiniteMana) return;
        currentManaValue = Mathf.Clamp(currentManaValue - value, 0, maxManaValue);
    }

    public void RestoreMana(float value)
    {
        currentManaValue = Mathf.Clamp(currentManaValue + value, 0, maxManaValue);
    }

    private void Update()
    {
        if (effects != null && effects.IsInfiniteMana)
        {
            currentManaValue = maxManaValue; // и полоска в UI всегда полная
            return;
        }

        if (currentManaValue != maxManaValue && !isSpending)
        {
            currentManaValue = Mathf.Clamp(currentManaValue + fillingSpeed * Time.deltaTime, 0, maxManaValue);
        }
    }
}