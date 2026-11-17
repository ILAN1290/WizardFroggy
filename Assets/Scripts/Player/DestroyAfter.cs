using UnityEngine;

public class DestroyAfter : MonoBehaviour
{
    public float lifetime = 1f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}