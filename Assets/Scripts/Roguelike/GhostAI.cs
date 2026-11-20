using UnityEngine;

/// <summary>
/// Movement and positioning brain for roguelike ghosts.
/// Melee ghosts flank the player and close gaps; ranged ghosts hold a firing
/// distance, retreat when pressured and circle to seek a clear line of sight.
/// Both styles steer around solid room geometry instead of walking directly
/// into shelves and walls.
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

    private Rigidbody2D body;
    private CircleCollider2D bodyCollider;
    private Transform player;
    private bool melee;
    private bool miniBoss;
    private float moveSpeed;
    private float nextFlankSwitch;
    private int flankSide = 1;

    public bool CanAttack { get; private set; }
    public Vector2 DirectionToPlayer { get; private set; }
    public float DistanceToPlayer { get; private set; }

    public void Initialize(Transform target, bool isMelee, bool isMiniBoss)
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<CircleCollider2D>();
        player = target != null ? target : FindPlayer();
        melee = isMelee;
        miniBoss = isMiniBoss;
        moveSpeed = isMelee ? (isMiniBoss ? 2.0f : 2.4f) : 1.7f;
        flankSide = Random.value < 0.5f ? -1 : 1;
        nextFlankSwitch = Time.time + flankSwitchInterval;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<CircleCollider2D>();
    }

    private void Update()
    {
        if (body == null) return;
        if (player == null) player = FindPlayer();
        if (player == null) return;

        Vector2 toPlayer = (Vector2)player.position - body.position;
        DistanceToPlayer = toPlayer.magnitude;
        DirectionToPlayer = DistanceToPlayer > 0.001f
            ? toPlayer / DistanceToPlayer
            : Vector2.up;

        if (Time.time >= nextFlankSwitch)
        {
            flankSide *= -1;
            nextFlankSwitch = Time.time + flankSwitchInterval * Random.Range(0.8f, 1.25f);
        }

        CanAttack = HasAttackRange(DistanceToPlayer) && HasLineOfSight();

        Vector2 desired = GetDesiredMovement(DirectionToPlayer, DistanceToPlayer);
        if (desired.sqrMagnitude < 0.001f) return;

        Vector2 steering = FindOpenDirection(desired.normalized);
        if (steering.sqrMagnitude > 0.001f)
            body.MovePosition(body.position + steering * moveSpeed * Time.deltaTime);
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
            // Once close, circle to the player's side rather than piling directly
            // onto their position. The flank direction changes every few seconds.
            return (tangent + towardPlayer * 0.12f).normalized;
        }

        // Ranged ghosts try to keep a firing lane between 4.5 and 6 units.
        if (distance > 6.2f) return towardPlayer;
        if (distance < 3.4f) return -towardPlayer;
        return (tangent + towardPlayer * 0.08f).normalized;
    }

    private Vector2 FindOpenDirection(Vector2 desired)
    {
        const float angleStep = 35f;
        Vector2 bestDirection = desired;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < 9; i++)
        {
            int offset = i == 0 ? 0 : ((i + 1) / 2) * (i % 2 == 1 ? 1 : -1);
            Vector2 candidate = Rotate(desired, offset * angleStep);
            float clearance = lookAheadDistance;
            bool blocked = false;

            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                body.position, obstacleProbeRadius, candidate,
                lookAheadDistance, obstacleMask);

            for (int h = 0; h < hits.Length; h++)
            {
                Collider2D obstacle = hits[h].collider;
                if (obstacle == null || obstacle.isTrigger) continue;
                if (obstacle.transform == transform ||
                    obstacle.transform.IsChildOf(transform)) continue;
                if (player != null && (obstacle.transform == player ||
                    obstacle.transform.IsChildOf(player))) continue;

                blocked = true;
                clearance = Mathf.Min(clearance, hits[h].distance);
            }

            float alignment = Vector2.Dot(candidate, desired);
            float score = alignment + clearance / Mathf.Max(0.01f, lookAheadDistance) * steeringWeight;
            if (!blocked) score += 0.35f;
            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = candidate;
            }
        }

        return bestDirection.normalized;
    }

    private bool HasLineOfSight()
    {
        if (player == null) return false;
        RaycastHit2D[] hits = Physics2D.LinecastAll(body.position, player.position, obstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D obstacle = hits[i].collider;
            if (obstacle == null || obstacle.isTrigger) continue;
            if (obstacle.transform == transform ||
                obstacle.transform.IsChildOf(transform)) continue;
            if (obstacle.transform == player || obstacle.transform.IsChildOf(player)) continue;
            return false;
        }
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
