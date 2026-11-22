using UnityEngine;

/// <summary>
/// A room door that blocks the passage until the room is cleared.
/// Its trigger remains slightly inside the room so the player can transition
/// immediately after the door unlocks.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class RoguelikeRoomTransition : MonoBehaviour
{
    private RoguelikeSession session;
    private int directionX;
    private int directionY;
    private Vector2 spawnPosition;
    private BoxCollider2D blocker;
    private BoxCollider2D sensor;
    private SpriteRenderer[] visuals;
    private Sprite[] closedSprites;
    private Sprite[] openedSprites;
    private Color lockedTint = Color.gray;
    private Color openedTint = Color.white;
    private bool hideWhenOpened;
    private bool transitionRequested;
    private bool lastOpened;

    public void Initialize(RoguelikeSession owner, int dx, int dy, Vector2 spawn,
        BoxCollider2D solidBlocker, BoxCollider2D transitionSensor,
        SpriteRenderer[] doorVisuals, Sprite[] closed, Sprite[] opened,
        Color closedTint, Color openTint, bool hideVisualWhenOpened)
    {
        session = owner;
        directionX = dx;
        directionY = dy;
        spawnPosition = spawn;
        blocker = solidBlocker;
        sensor = transitionSensor;
        visuals = doorVisuals;
        closedSprites = closed;
        openedSprites = opened;
        lockedTint = closedTint;
        openedTint = openTint;
        hideWhenOpened = hideVisualWhenOpened;

        if (sensor != null) sensor.isTrigger = true;
        RefreshDoor(true);
    }

    private void Update()
    {
        RefreshDoor(false);
    }

    private void RefreshDoor(bool force)
    {
        bool opened = session != null && session.IsCurrentRoomCleared;
        if (!force && opened == lastOpened) return;
        lastOpened = opened;

        if (blocker != null)
            blocker.enabled = !opened;

        if (visuals == null) return;
        for (int i = 0; i < visuals.Length; i++)
        {
            SpriteRenderer renderer = visuals[i];
            if (renderer == null) continue;

            if (opened && hideWhenOpened)
            {
                renderer.enabled = false;
                continue;
            }

            Sprite sprite = null;
            Sprite[] source = opened ? openedSprites : closedSprites;
            if (source != null && i < source.Length)
                sprite = source[i];

            if (sprite != null) renderer.sprite = sprite;
            renderer.color = opened ? openedTint : lockedTint;
            renderer.enabled = renderer.sprite != null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // The trigger is fitted to the visible door sprite, so only doorway contact
        // can begin a transition.
        if (!IsPlayer(other) || session == null) return;

        if (!session.IsCurrentRoomCleared)
        {
            session.TryMoveByDirection(directionX, directionY, spawnPosition);
            return;
        }

        RequestTransition();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Waiting at a locked doorway is allowed: once the room is cleared,
        // staying inside the door's sprite-sized trigger completes the transition.
        // The arrival point is calculated outside this trigger, so this cannot
        // bounce the player back and forth between rooms.
        if (!IsPlayer(other) || session == null || !session.IsCurrentRoomCleared)
            return;

        RequestTransition();
    }

    private void RequestTransition()
    {
        if (transitionRequested) return;
        transitionRequested = true;
        session.TryMoveByDirection(directionX, directionY, spawnPosition);
    }

    private static bool IsPlayer(Collider2D other)
    {
        // Only the main Player-tagged collider can activate a door.
        // Child hitboxes (parry, shields and attack effects) must not trigger travel.
        return other != null && other.CompareTag("Player");
    }
}
