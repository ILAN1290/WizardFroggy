using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D))]
public class TowerDefenceMushroomPickup : MonoBehaviour
{
    private TowerDefenceManager manager;
    private float lifetime;
    private float age;
    private Vector3 basePosition;

    public void Initialize(TowerDefenceManager owner, float lifeSeconds)
    {
        manager = owner;
        lifetime = Mathf.Max(1f, lifeSeconds);
        basePosition = transform.position;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float bob = Mathf.Sin(age * 4f) * 0.08f;
        transform.position = basePosition + Vector3.up * bob;

        if (age >= lifetime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (manager == null || other == null)
            return;

        if (!other.CompareTag("Player") &&
            other.GetComponentInParent<PlayerController>() == null)
            return;

        manager.CollectMushroom();
        Destroy(gameObject);
    }
}
