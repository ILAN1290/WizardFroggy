using UnityEngine;

/// <summary>
/// Movement and positioning brain for roguelike ghosts.
/// Physics queries run at a limited rate and reuse a fixed hit buffer to avoid
/// per-frame allocations while preserving obstacle avoidance.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class GhostAI : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField, Min(0.1f)] private float lookAheadDistance = 0.9f;
    [SerializeField, Min(0.05f)] private float obstacleProbeRadius = 0.38f;
    [SerializeField, Min(0f)] private float steeringWeight = 0.6f;
    [SerializeField, Min(0.25f)] private float flankSwitchInterval = 2.6f;
    [SerializeField, Min(0.04f)] private float obstacleCheckInterval = 0.12f;
    [SerializeField, Min(0.04f)] private float lineOfSightCheckInterval = 0.12f;

    private static readonly float[] SteeringAngles = { -70f, -35f, 35f, 70f, -110f, 110f };
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[8];

    private Rigidbody2D body;
    private Transform player;
    private bool melee;
    private bool miniBoss;
    private float moveSpeed;
    private float nextFlankSwitch;
    private float nextObstacleCheck;
    private float nextLineOfSightCheck;
    private float nextPlayerSearch;
    private int flankSide = 1;
    private bool canSeePlayer;
    private Vector2 cachedSteering;

    public bool CanAttack { get; private set; }
    public Vector2 DirectionToPlayer { get; private set; }
    public float DistanceToPlayer { get; private set; }

    public void Initialize(Transform target, bool isMelee, bool isMiniBoss)
    {
        body = GetComponent<Rigidbody2D>();
        player = target != null ? target : FindPlayer();
        melee = isMelee;
        miniBoss = isMiniBoss;
        moveSpeed = isMelee ? (isMiniBoss ? 2.0f : 2.4f) : 1.7f;
        flankSide = Random.value < 0.5f ? -1 : 1;
        nextFlankSwitch = Time.time + flankSwitchInterval;
        nextObstacleCheck = Time.time;
        nextLineOfSightCheck = Time.time;
        nextPlayerSearch = Time.time + 1f;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (body == null) return;

        if (player == null && Time.time >= nextPlayerSearch)
        {
            player = FindPlayer();
            nextPlayerSearch = Time.time + 1f;
        }

        if (player == null)
        {
            CanAttack = false;
            cachedSteering = Vector2.zero;
            return;
        }

        Vector2 toPlayer = (Vector2)player.position - body.position;
        float distanceSquared = toPlayer.sqrMagnitude;
        DistanceToPlayer = Mathf.Sqrt(distanceSquared);
        DirectionToPlayer = distanceSquared > 0.000001f
            ? toPlayer / DistanceToPlayer
            : Vector2.up;

        if (Time.time >= nextFlankSwitch)
        {
            flankSide *= -1;
            nextFlankSwitch = Time.time +
                flankSwitchInterval * Random.Range(0.8f, 1.25f);
        }

        bool inAttackRange = HasAttackRange(DistanceToPlayer);
        if (Time.time >= nextLineOfSightCheck)
        {
            canSeePlayer = inAttackRange && HasLineOfSight();
            nextLineOfSightCheck = Time.time + lineOfSightCheckInterval;
        }
        CanAttack = inAttackRange && canSeePlayer;

        Vector2 desired = GetDesiredMovement(DirectionToPlayer, DistanceToPlayer);
        if (Time.time >= nextObstacleCheck)
        {
            cachedSteering = desired.sqrMagnitude > 0.0001f
                ? FindOpenDirection(desired.normalized)
                : Vector2.zero;
            nextObstacleCheck = Time.time + obstacleCheckInterval;
        }

        if (cachedSteering.sqrMagnitude > 0.0001f)
        {
            body.MovePosition(body.position +
                cachedSteering * moveSpeed * Time.fixedDeltaTime);
        }
    }

    private bool HasAttackRange(float distance)
    {
        if (melee)
            return distance <= (miniBoss ? 1.75f : 1.5f);

        return distance >= 3.0f && distance <= 8.5f;
    }

    private Vector2 GetDesiredMovement(Vector2 towardPlayer, float distance)
    {
        Vector2 tangent = new Vector2(-towardPlayer.y, towardPlayer.x) * flankSide;

        if (melee)
        {
            if (distance > 1.7f) return towardPlayer;
            if (distance < 0.95f) return -towardPlayer;
            return (tangent + towardPlayer * 0.12f).normalized;
        }

        if (distance > 6.2f) return towardPlayer;
        if (distance < 3.4f) return -towardPlayer;
        return (tangent + towardPlayer * 0.08f).normalized;
    }

    private Vector2 FindOpenDirection(Vector2 desired)
    {
        if (!IsDirectionBlocked(desired, out float directClearance))
            return desired;

        Vector2 bestDirection = desired;
        float bestScore = ScoreDirection(desired, desired, directClearance, true);

        // Only fan out when the direct path is blocked. Most frames and most
        // Most frames therefore need only one non-allocating cast.
        for (int i = 0; i < SteeringAngles.Length; i++)
        {
            Vector2 candidate = Rotate(desired, SteeringAngles[i]);
            bool blocked = IsDirectionBlocked(candidate, out float clearance);
            float score = ScoreDirection(candidate, desired, clearance, blocked);
            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = candidate;
            }
        }

        return bestDirection.normalized;
    }

    private float ScoreDirection(Vector2 candidate, Vector2 desired,
        float clearance, bool blocked)
    {
        float score = Vector2.Dot(candidate, desired) +
            clearance / Mathf.Max(0.01f, lookAheadDistance) * steeringWeight;
        return blocked ? score : score + 0.35f;
    }

    private bool IsDirectionBlocked(Vector2 direction, out float clearance)
    {
        clearance = lookAheadDistance;
        int hitCount = Physics2D.CircleCastNonAlloc(
            body.position, obstacleProbeRadius, direction,
            hitBuffer, lookAheadDistance, obstacleMask);

        bool blocked = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D obstacle = hitBuffer[i].collider;
            if (!IsRelevantObstacle(obstacle)) continue;

            blocked = true;
            clearance = Mathf.Min(clearance, hitBuffer[i].distance);
        }

        return blocked;
    }

    private bool HasLineOfSight()
    {
        if (player == null) return false;

        int hitCount = Physics2D.LinecastNonAlloc(
            body.position, player.position, hitBuffer, obstacleMask);

        for (int i = 0; i < hitCount; i++)
        {
            if (IsRelevantObstacle(hitBuffer[i].collider))
                return false;
        }

        return true;
    }

    private bool IsRelevantObstacle(Collider2D obstacle)
    {
        if (obstacle == null || obstacle.isTrigger) return false;

        Transform obstacleTransform = obstacle.transform;
        if (obstacleTransform == transform || obstacleTransform.IsChildOf(transform))
            return false;

        if (player != null && (obstacleTransform == player ||
            obstacleTransform.IsChildOf(player)))
            return false;

        return true;
    }

    private Transform FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            vector.x * cosine - vector.y * sine,
            vector.x * sine + vector.y * cosine);
    }
}
