using UnityEngine;

public class RoguelikePickup : MonoBehaviour
{
    private Item item;
    private int count;
    private int currency;
    private AudioClip pickupSound;

    private SpriteRenderer[] animatedRenderers;
    private Vector3[] visualBasePositions;
    private Quaternion[] visualBaseRotations;
    private Vector3[] visualBaseScales;
    private float animationPhase;

    private void Awake()
    {
        CacheVisuals();
    }

    private void CacheVisuals()
    {
        if (animatedRenderers != null)
            return;

        animatedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        visualBasePositions = new Vector3[animatedRenderers.Length];
        visualBaseRotations = new Quaternion[animatedRenderers.Length];
        visualBaseScales = new Vector3[animatedRenderers.Length];
        animationPhase = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < animatedRenderers.Length; i++)
        {
            Transform visual = animatedRenderers[i].transform;
            visualBasePositions[i] = visual.localPosition;
            visualBaseRotations[i] = visual.localRotation;
            visualBaseScales[i] = visual.localScale;
        }
    }

    private void Update()
    {
        CacheVisuals();

        // Мягкое покачивание и лёгкий наклон делают добычу заметной,
        // при этом сами коллайдеры остаются на месте.
        float time = Time.time + animationPhase;
        float bob = Mathf.Sin(time * 3.4f) * 0.055f;
        float tilt = Mathf.Sin(time * 2.2f) * 3f;
        float pulse = 1f + Mathf.Sin(time * 3f) * 0.018f;

        for (int i = 0; i < animatedRenderers.Length; i++)
        {
            SpriteRenderer renderer = animatedRenderers[i];
            if (renderer == null)
                continue;

            Transform visual = renderer.transform;
            visual.localPosition = visualBasePositions[i] + Vector3.up * bob;
            visual.localRotation = visualBaseRotations[i] *
                Quaternion.Euler(0f, 0f, tilt);
            visual.localScale = visualBaseScales[i] * pulse;
        }
    }

    public void SetupItem(Item source, int amount, AudioClip sound = null)
    {
        item = source;
        count = Mathf.Max(1, amount);
        pickupSound = sound;
    }

    public void SetupCurrency(int amount, AudioClip sound = null)
    {
        currency = Mathf.Max(1, amount);
        pickupSound = sound;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (currency > 0)
        {
            PlayerRunData.EnsureInstance().AddMushrooms(currency);
            if (RoguelikeSession.Instance != null)
                RoguelikeSession.Instance.ShowHint("Получено грибочков: " + currency);
            if (pickupSound != null)
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, 0.55f);
            Destroy(gameObject);
            return;
        }

        Inventory inventory = Inventory.EnsureInstance();
        if (inventory != null && inventory.Size == 0)
            inventory.Init(20);

        if (inventory != null && item != null && inventory.Add(item, count))
        {
            if (TowerDefenceManager.Instance != null &&
                TowerDefenceManager.Instance.IsGameSessionRunning)
                TowerDefenceManager.Instance.ShowStatusMessage("Получено: " + item.itemName);
            else if (RoguelikeSession.Instance != null)
                RoguelikeSession.Instance.ShowHint("Получен предмет: " + item.itemName);
            if (pickupSound != null)
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, 0.55f);
            Destroy(gameObject);
        }
        else if (RoguelikeSession.Instance != null)
        {
            RoguelikeSession.Instance.ShowHint("Инвентарь заполнен!");
        }
    }
}
