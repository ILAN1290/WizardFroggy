using UnityEngine;

public class RoguelikeDoor : MonoBehaviour
{
    private RoguelikeSession session;
    private int dx;
    private int dy;
    private Vector2 spawnPosition;

    private SpriteRenderer[] visualTiles;
    private Sprite[] closedSprites;
    private Sprite[] openedSprites;
    private SpriteRenderer fallbackRenderer;
    private Collider2D solidBlocker;
    private Collider2D transitionTrigger;
    private bool lastUnlocked;
    private bool initialized;

    public void Initialize(RoguelikeSession owner, int directionX, int directionY,
        Vector2 spawn, SpriteRenderer[] parts, Sprite[] closed, Sprite[] opened,
        SpriteRenderer fallback, Collider2D blocker, Collider2D trigger)
    {
        session = owner;
        dx = directionX;
        dy = directionY;
        spawnPosition = spawn;
        visualTiles = parts;
        closedSprites = closed;
        openedSprites = opened;
        fallbackRenderer = fallback;
        solidBlocker = blocker;
        transitionTrigger = trigger;
        initialized = true;
        RefreshDoor(true);
    }

    private void Update()
    {
        if (initialized) RefreshDoor(false);
    }

    private void RefreshDoor(bool force)
    {
        bool unlocked = session != null && session.IsCurrentRoomCleared;
        if (!force && unlocked == lastUnlocked) return;
        lastUnlocked = unlocked;

        if (solidBlocker != null) solidBlocker.enabled = !unlocked;
        if (transitionTrigger != null) transitionTrigger.enabled = unlocked;

        bool needsFallback = visualTiles == null || visualTiles.Length == 0;
        if (visualTiles != null)
        {
            for (int i = 0; i < visualTiles.Length; i++)
            {
                SpriteRenderer part = visualTiles[i];
                if (part == null) continue;

                Sprite sprite = null;
                if (unlocked && openedSprites != null && i < openedSprites.Length)
                    sprite = openedSprites[i];
                else if (!unlocked && closedSprites != null && i < closedSprites.Length)
                    sprite = closedSprites[i];

                if (sprite != null)
                {
                    part.sprite = sprite;
                    part.color = Color.white;
                    part.enabled = true;
                }
                else
                {
                    part.enabled = false;
                    needsFallback = true;
                }
            }
        }

        if (fallbackRenderer != null)
        {
            fallbackRenderer.enabled = needsFallback;
            fallbackRenderer.color = unlocked
                ? new Color(0.32f, 0.72f, 0.58f, 0.85f)
                : new Color(0.42f, 0.2f, 0.18f, 1f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (session == null || other == null) return;

        bool isPlayer = other.CompareTag("Player") ||
                        other.GetComponentInParent<PlayerHealth>() != null;
        if (isPlayer)
            session.TryMoveByDirection(dx, dy, spawnPosition);
    }
}
