using UnityEngine;

public class RoguelikeChest : MonoBehaviour
{
    private RoguelikeSession session;
    private bool opened;

    public void Initialize(RoguelikeSession owner)
    {
        session = owner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryOpen(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Stay is a fallback for cases where the player was already overlapping
        // the chest sensor when the room finished generating.
        TryOpen(other);
    }

    private void TryOpen(Collider2D other)
    {
        if (opened || other == null || !other.CompareTag("Player")) return;

        opened = true;
        if (session != null)
            session.OpenChest(transform.position);

        Destroy(gameObject);
    }
}
