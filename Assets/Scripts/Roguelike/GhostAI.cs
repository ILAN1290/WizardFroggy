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
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[16];
    private readonly Collider2D[] nearbyGhostBuffer = new Collider2D[16];

    private Rigidbody2D body;
    private CircleCollider2D bodyCollider;
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
        bodyCollider = GetComponent<CircleCollider2D>();
        if (body != null)
            body.useFullKinematicContacts = true;
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
        bodyCollider = GetComponent<CircleCollider2D>();
        if (body != null)
            body.useFullKinematicContacts = true;
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
        Vector2 separation = GetGhostSeparation();
        if (separation.sqrMagnitude > 0.0001f)
        {
            // A close neighbour takes priority over chasing the player.
            // This prevents groups of ghosts from locking against each other.
            float separationStrength = 1.2f + Mathf.Clamp01(separation.magnitude) * 0.8f;
            desired = (desired + separation.normalized * separationStrength).normalized;
        }

        if (Time.time >= nextObstacleCheck || separation.sqrMagnitude > 0.01f)
        {
            cachedSteering = desired.sqrMagnitude > 0.0001f
                ? FindOpenDirection(desired.normalized)
                : Vector2.zero;
            nextObstacleCheck = Time.time + obstacleCheckInterval;
        }

        if (cachedSteering.sqrMagnitude > 0.0001f)
        {
            Vector2 movementDirection = cachedSteering.normalized;
            float requestedDistance = moveSpeed * Time.fixedDeltaTime;
            float safeDistance = GetSafeMovementDistance(
                movementDirection, requestedDistance);

            if (safeDistance > 0.001f)
                body.MovePosition(body.position + movementDirection * safeDistance);
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
            body.position, GetProbeRadius(), direction,
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

    private Vector2 GetGhostSeparation()
    {
        float ownRadius = GetProbeRadius();
        int count = Physics2D.OverlapCircleNonAlloc(
            body.position, ownRadius * 5f, nearbyGhostBuffer, obstacleMask);

        Vector2 separation = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Collider2D nearby = nearbyGhostBuffer[i];
            if (nearby == null || nearby.isTrigger) continue;

            GhostAI other = nearby.GetComponentInParent<GhostAI>();
            if (other == null || other == this || other.body == null) continue;

            Vector2 away = body.position - other.body.position;
            float distance = away.magnitude;
            float preferredDistance = ownRadius + other.GetProbeRadius() + 0.12f;
            float separationRange = preferredDistance * 2.2f;
            if (distance >= separationRange) continue;

            if (distance < 0.001f)
            {
                // A deterministic pair-specific direction prevents three or
                // more coincident ghosts from cancelling each other's separation.
                int ownId = GetInstanceID();
                int otherId = other.GetInstanceID();
                int firstId = Mathf.Min(ownId, otherId);
                int secondId = Mathf.Max(ownId, otherId);
                uint pairHash;
                unchecked
                {
                    pairHash = (uint)(firstId * 73856093) ^
                        (uint)(secondId * 19349663);
                }

                float angle = (pairHash % 360u) * Mathf.Deg2Rad;
                Vector2 pairDirection = new Vector2(
                    Mathf.Cos(angle), Mathf.Sin(angle));
                away = ownId < otherId ? pairDirection : -pairDirection;
                distance = 0.001f;
            }
            else
            {
                away /= distance;
            }

            float strength = (separationRange - distance) /
                Mathf.Max(0.01f, preferredDistance);
            separation += away * strength;
        }

        return separation;
    }

    private float GetProbeRadius()
    {
        if (bodyCollider == null) return obstacleProbeRadius;

        Vector3 scale = transform.lossyScale;
        float scaleFactor = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        return Mathf.Max(obstacleProbeRadius, bodyCollider.radius * scaleFactor);
    }

    private float GetSafeMovementDistance(Vector2 direction, float distance)
    {
        if (distance <= 0f) return 0f;

        // Steering can choose a detour, but every physics step is independently
        // swept so a ghost cannot tunnel through a wall or a shelf.
        int hitCount = Physics2D.CircleCastNonAlloc(
            body.position, GetProbeRadius(), direction,
            hitBuffer, distance + 0.03f, obstacleMask);

        float safeDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            if (!IsRelevantObstacle(hitBuffer[i].collider)) continue;
            safeDistance = Mathf.Min(safeDistance,
                Mathf.Max(0f, hitBuffer[i].distance - 0.025f));
        }

        return safeDistance;
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

        // Ghosts are handled by the separation steering above, not by wall casts.
        // Treating another ghost as a hard obstacle can deadlock two overlapping
        // kinematic bodies because every cast starts inside the neighbour collider.
        if (obstacle.GetComponentInParent<GhostAI>() != null)
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
