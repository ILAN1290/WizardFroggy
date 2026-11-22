using UnityEngine;

/// <summary>
/// Non-blocking room-edge sensor. It replaces the old solid door and wall
/// assembly while keeping navigation between connected rooms available.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class RoguelikeRoomTransition : MonoBehaviour
{
    private RoguelikeSession session;
    private int directionX;
    private int directionY;
    private Vector2 spawnPosition;

    public void Initialize(RoguelikeSession owner, int dx, int dy, Vector2 spawn)
    {
        session = owner;
        directionX = dx;
        directionY = dy;
        spawnPosition = spawn;

        BoxCollider2D sensor = GetComponent<BoxCollider2D>();
        sensor.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other) || session == null) return;
        session.TryMoveByDirection(directionX, directionY, spawnPosition);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // If the player reached the edge before clearing the room, allow the
        // transition as soon as the last enemy is defeated.
        if (!IsPlayer(other) || session == null || !session.IsCurrentRoomCleared)
            return;

        session.TryMoveByDirection(directionX, directionY, spawnPosition);
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other != null && (other.CompareTag("Player") ||
            other.GetComponentInParent<PlayerHealth>() != null);
    }
}
