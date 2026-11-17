using UnityEngine;

public class RoguelikeDoor : MonoBehaviour
{
    private RoguelikeSession session;
    private int dx;
    private int dy;
    private Vector2 spawnPosition;

    public void Initialize(RoguelikeSession owner, int directionX, int directionY, Vector2 spawn)
    {
        session = owner;
        dx = directionX;
        dy = directionY;
        spawnPosition = spawn;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || session == null) return;
        session.TryMoveByDirection(dx, dy, spawnPosition);
    }
}
