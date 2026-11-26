using System.Collections;
using UnityEngine;

public class GhostEnemy : MonoBehaviour, IDamageable
{
    private RoguelikeSession session;
    private Transform player;
    private PlayerHealth playerHealth;
    private GhostAI ai;
    private float health;
    private float damage;
    private bool melee;
    private bool miniBoss;
    private float nextAttackTime;
    private float nextRadialShotTime;
    private bool defeated;
    private Sprite[] attackSprites;
    private Transform effectsRoot;
    private Transform projectilesRoot;
    private Rigidbody2D body;
    private int attacksSinceSuper;
    private float nextMeleeSuperTime;
    private float nextGhostVoiceTime;
    private bool superAttackInProgress;

    public void Initialize(RoguelikeSession owner, int floor, bool isMelee,
        bool isMiniBoss = false, Sprite[] attackAnimationSprites = null,
        Transform playerTarget = null, Transform effectsParent = null,
        Transform projectilesParent = null)
    {
        session = owner;
        melee = isMelee;
        miniBoss = isMiniBoss;
        health = isMiniBoss ? 125f + floor * 25f : 22f + floor * 8f;
        damage = isMiniBoss ? 16f + floor * 3f : 8f + floor * 2.5f;
        attackSprites = attackAnimationSprites != null
            ? (Sprite[])attackAnimationSprites.Clone()
            : new Sprite[0];

        player = playerTarget;
        effectsRoot = effectsParent;
        projectilesRoot = projectilesParent;
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        body = GetComponent<Rigidbody2D>();
        ai = GetComponent<GhostAI>();
        if (ai == null) ai = gameObject.AddComponent<GhostAI>();
        ai.Initialize(player, melee, miniBoss);

        nextRadialShotTime = Time.time + 2.5f;
        // Independent timers keep both dashes and ambient ghost voices from
        // happening in sync when several enemies spawn together.
        nextMeleeSuperTime = Time.time + Random.Range(8f, 12f);
        nextGhostVoiceTime = Time.time + Random.Range(5f, 13f);
    }

    private IEnumerator Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();

        // PlayerController creates its thin movement collider in Start().
        // Wait one frame, then ignore every player/ghost collider pair.
        yield return null;
        IgnorePlayerCollisionPairs();
        yield return new WaitForFixedUpdate();
        IgnorePlayerCollisionPairs();
    }

    private void IgnorePlayerCollisionPairs()
    {
        if (player == null) return;

        Collider2D[] playerColliders =
            player.GetComponentsInChildren<Collider2D>(true);
        Collider2D[] ghostColliders =
            GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < ghostColliders.Length; i++)
        {
            Collider2D ghostCollider = ghostColliders[i];
            if (ghostCollider == null) continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider2D playerCollider = playerColliders[j];
                if (playerCollider == null || playerCollider == ghostCollider)
                    continue;

                Physics2D.IgnoreCollision(ghostCollider, playerCollider, true);
            }
        }
    }

    private void Update()
    {
        if (defeated) return;

        if (Time.time >= nextGhostVoiceTime)
        {
            if (session != null)
                session.PlayGhostVoiceSound();
            nextGhostVoiceTime = Time.time + Random.Range(8f, 12f);
        }

        if (player == null || ai == null || superAttackInProgress)
            return;

        if (miniBoss && Time.time >= nextRadialShotTime)
        {
            nextRadialShotTime = Time.time + 4.5f;
            if (session != null) session.PlayGhostAttackSound(false);
            ShootRadialBurst();
        }

        // The dash cooldown is checked before the regular attack gate.
        // Melee ghosts and the mini-boss can therefore dash while waiting
        // for their next normal attack or while its cooldown is running.
        if ((melee || miniBoss) && Time.time >= nextMeleeSuperTime)
        {
            Vector2 dashDirection = ai.DirectionToPlayer;
            // Each ghost keeps its own timer; average interval remains ten seconds.
            nextMeleeSuperTime = Time.time + Random.Range(8f, 12f);
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 2.6f);
            StartCoroutine(PerformMeleeDash(dashDirection));
            return;
        }

        if (!ai.CanAttack || Time.time < nextAttackTime) return;

        Vector2 direction = ai.DirectionToPlayer;

        if (miniBoss && attacksSinceSuper >= 3 &&
            ai.DistanceToPlayer <= 1.75f)
        {
            attacksSinceSuper = 0;
            nextAttackTime = Time.time + 3.2f;

            // The mini-boss randomly chooses one of its two charged specials.
            if (Random.value < 0.5f)
            {
                StartCoroutine(PerformGroundSlam());
            }
            else
            {
                if (session != null) session.PlayGhostAttackSound(true);
                StartCoroutine(PerformSpiralAttack(direction, 36, 13f, 0.05f));
            }
            return;
        }

        // At range, the mini-boss fires ordinary aimed shots and stores one
        // charge per attack. Reaching three charges does not trigger the slam:
        // it keeps chasing and waits until it gets close to the player.
        if (miniBoss && ai.DistanceToPlayer > 1.75f)
        {
            attacksSinceSuper = Mathf.Min(attacksSinceSuper + 1, 3);
            nextAttackTime = Time.time + 1.8f;
            if (session != null) session.PlayGhostAttackSound(false);
            StartCoroutine(PlayAttackAnimation(direction));
            Shoot(direction);
            return;
        }

        if (melee)
        {
            nextAttackTime = Time.time + 1.15f;
            if (session != null) session.PlayGhostAttackSound(true);
            StartCoroutine(PlayAttackAnimation(direction));
            if (playerHealth != null) playerHealth.TakeDamage(damage);
        }
        else
        {
            if (!miniBoss)
                attacksSinceSuper++;

            if (!miniBoss && attacksSinceSuper >= 4)
            {
                attacksSinceSuper = 0;
                nextAttackTime = Time.time + 2.7f;
                if (session != null) session.PlayGhostAttackSound(false);
                StartCoroutine(PerformSpiralAttack(direction));
                return;
            }

            nextAttackTime = Time.time + 1.8f;
            if (session != null) session.PlayGhostAttackSound(false);
            StartCoroutine(PlayAttackAnimation(direction));
            Shoot(direction);
        }
    }

    private IEnumerator PlayAttackAnimation(Vector2 direction)
    {
        if (attackSprites == null || attackSprites.Length == 0) yield break;

        GameObject effect = new GameObject("Анимация атаки призрака");
        Transform parent = effectsRoot != null ? effectsRoot : transform.parent;
        if (parent != null) effect.transform.SetParent(parent, true);
        effect.transform.position = transform.position + (Vector3)(direction * 0.78f);
        effect.transform.rotation = Quaternion.Euler(0f, 0f,
            Vector2.SignedAngle(Vector2.right, direction));

        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 80;
        renderer.enabled = true;

        const float frameTime = 0.11f;
        Destroy(effect, attackSprites.Length * frameTime + 0.2f);
        float targetHeight = 1.35f * (miniBoss ? 1.2f : 1f);

        for (int i = 0; i < attackSprites.Length; i++)
        {
            Sprite frame = attackSprites[i];
            if (frame == null) continue;

            renderer.sprite = frame;
            renderer.color = miniBoss
                ? new Color(1f, 0.36f, 0.4f, 1f)
                : new Color(1f, 1f, 1f, 1f);

            float sourceHeight = Mathf.Max(0.001f, frame.bounds.size.y);
            effect.transform.localScale = Vector3.one * (targetHeight / sourceHeight);

            yield return new WaitForSeconds(frameTime);
        }

        Destroy(effect);
    }

    private IEnumerator PerformSpiralAttack(Vector2 initialDirection,
        int projectileCount = 24, float angleStep = 22f, float shotInterval = 0.045f)
    {
        superAttackInProgress = true;
        ai.SetMovementSuspended(true);
        yield return StartCoroutine(PlayAttackAnimation(initialDirection));

        float angle = Mathf.Atan2(initialDirection.y, initialDirection.x) *
            Mathf.Rad2Deg;

        for (int i = 0; i < projectileCount && !defeated; i++)
        {
            float radians = (angle + i * angleStep) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(
                Mathf.Cos(radians), Mathf.Sin(radians));
            Shoot(direction);
            yield return new WaitForSeconds(shotInterval);
        }

        ai.SetMovementSuspended(false);
        superAttackInProgress = false;
    }

    private IEnumerator PerformMeleeDash(Vector2 direction)
    {
        superAttackInProgress = true;
        if (session != null) session.PlayGhostAttackSound(true);
        ai.SetMovementSuspended(true);
        yield return new WaitForSeconds(0.08f);

        if (body == null)
            body = GetComponent<Rigidbody2D>();

        Vector2 dashOrigin = body != null
            ? body.position : (Vector2)transform.position;

        // Re-aim at the player's current position immediately before the dash.
        if (player != null)
        {
            Vector2 toPlayer = (Vector2)player.position - dashOrigin;
            if (toPlayer.sqrMagnitude > 0.0001f)
                direction = toPlayer.normalized;
        }
        else if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
        }
        else
        {
            direction = Vector2.up;
        }

        StartCoroutine(PlayDashVisual(direction));

        float pixelsPerUnit = Camera.main != null && Camera.main.orthographic
            ? Camera.main.pixelHeight /
                Mathf.Max(0.01f, Camera.main.orthographicSize * 2f)
            : Mathf.Max(1f, Screen.height) / 12f;
        float requestedDistance = 400f / Mathf.Max(1f, pixelsPerUnit);
        CircleCollider2D ownCollider = GetComponent<CircleCollider2D>();
        float radius = ownCollider != null
            ? ownCollider.radius * Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y))
            : 0.4f;

        // A dash is also a gap-closer: stop near the player instead of blindly
        // travelling the full distance and overshooting a nearby target.
        float distanceToPlayer = player != null
            ? Vector2.Distance(dashOrigin, player.position)
            : requestedDistance;
        float desiredStopGap = radius + 0.30f;
        float approachDistance = player != null
            ? Mathf.Max(0.25f, distanceToPlayer - desiredStopGap)
            : requestedDistance;
        float plannedDistance = Mathf.Min(requestedDistance, approachDistance);
        float dashDistance = GetDashClearDistance(
            direction, plannedDistance, radius);

        const float dashDuration = 0.24f;
        float speed = dashDistance / dashDuration;
        float travelled = 0f;
        bool damagedPlayer = false;

        while (travelled < dashDistance && !defeated)
        {
            yield return new WaitForFixedUpdate();
            float step = Mathf.Min(speed * Time.fixedDeltaTime,
                dashDistance - travelled);
            Vector2 nextPosition = body != null
                ? body.position + direction * step
                : (Vector2)transform.position + direction * step;

            if (body != null)
                body.MovePosition(nextPosition);
            else
                transform.position = nextPosition;

            travelled += step;

            if (!damagedPlayer && playerHealth != null && player != null &&
                Vector2.Distance(nextPosition, player.position) <= radius + 0.65f)
            {
                playerHealth.TakeDamage(damage * 1.5f);
                damagedPlayer = true;
            }
        }

        ai.SetMovementSuspended(false);
        superAttackInProgress = false;
    }

    private float GetDashClearDistance(
        Vector2 direction, float maxDistance, float radius)
    {
        if (maxDistance <= 0f) return 0f;

        Vector2 origin = body != null ? body.position : (Vector2)transform.position;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            origin, radius, direction, maxDistance);
        float safeDistance = maxDistance;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D obstacle = hits[i].collider;
            if (obstacle == null ||
                obstacle.transform == transform ||
                obstacle.transform.IsChildOf(transform))
                continue;

            if (obstacle.GetComponentInParent<RoguelikeGhostPassThrough>() != null)
                continue;
            if (obstacle.isTrigger || !obstacle.CompareTag("Wall"))
                continue;

            safeDistance = Mathf.Min(safeDistance,
                Mathf.Max(0f, hits[i].distance - 0.04f));
        }

        return safeDistance;
    }

    private IEnumerator PlayDashVisual(Vector2 direction)
    {
        if (attackSprites == null || attackSprites.Length == 0)
            yield break;

        GameObject effect = new GameObject("Рывок призрака • эффект");
        Transform parent = effectsRoot != null ? effectsRoot : transform.parent;
        if (parent != null) effect.transform.SetParent(parent, true);

        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 23;
        float angle = Vector2.SignedAngle(Vector2.right, direction);
        effect.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        for (int i = 0; i < attackSprites.Length; i++)
        {
            Sprite frame = attackSprites[i];
            if (frame == null) continue;

            renderer.sprite = frame;
            renderer.color = new Color(1f, 1f, 1f, 0.9f);
            float sourceHeight = Mathf.Max(0.001f, frame.bounds.size.y);
            effect.transform.localScale = Vector3.one * (1.35f / sourceHeight);
            effect.transform.position = transform.position +
                (Vector3)(direction * 0.42f);
            yield return new WaitForSeconds(0.055f);
        }

        Destroy(effect);
    }

    private IEnumerator PerformGroundSlam()
    {
        superAttackInProgress = true;
        ai.SetMovementSuspended(true);
        StartCoroutine(PlayAttackAnimation(Vector2.down));

        yield return new WaitForSeconds(0.42f);
        if (!defeated && session != null)
            session.TriggerMiniBossGroundSlam(
                transform.position, damage * 0.45f);

        yield return new WaitForSeconds(0.35f);
        ai.SetMovementSuspended(false);
        superAttackInProgress = false;
    }

    private void ShootRadialBurst()
    {
        const int projectileCount = 24;
        Color redTint = new Color(1f, 0.22f, 0.27f, 1f);

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = i * (360f / projectileCount);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));
            Shoot(direction, redTint, damage * 0.65f);
        }
    }

    private void Shoot(Vector2 direction)
    {
        Color tint = miniBoss
            ? new Color(1f, 0.22f, 0.27f, 1f)
            : new Color(0.68f, 0.9f, 1f, 1f);
        Shoot(direction, tint, damage);
    }

    private void Shoot(Vector2 direction, Color tint, float projectileDamage)
    {
        GameObject bolt = CreateProjectile(
            miniBoss ? "Красный снаряд мини-босса" : "Призрачный снаряд",
            transform.position, tint, 0.14f);

        BoxCollider2D collider = bolt.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = Vector2.one;

        Rigidbody2D projectileBody = bolt.AddComponent<Rigidbody2D>();
        projectileBody.bodyType = RigidbodyType2D.Kinematic;
        projectileBody.gravityScale = 0f;

        // Same dimensions, collider, glow, speed and comet trail as the blue bolts.
        bolt.AddComponent<RoguelikeProjectile>().Initialize(
            direction, 5.4f, projectileDamage, tint);
    }

    private GameObject CreateProjectile(string objectName, Vector2 position,
        Color tint, float size)
    {
        GameObject projectile = new GameObject(objectName);
        Transform parent = projectilesRoot != null ? projectilesRoot : transform.parent;
        if (parent != null) projectile.transform.SetParent(parent, true);
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one * size;

        SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
        renderer.sprite = RoomGenerator.WhiteSquareSprite;
        renderer.color = tint;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 8;
        return projectile;
    }

    public void DieFromWall()
    {
        if (defeated) return;

        defeated = true;
        if (session != null) session.OnEnemyDefeated(transform.position, miniBoss);
        Destroy(gameObject);
    }

    public void TakeDamage(float value)
    {
        if (defeated) return;
        health -= value;
        if (health <= 0f)
        {
            defeated = true;
            if (session != null) session.OnEnemyDefeated(transform.position, miniBoss);
            Destroy(gameObject);
        }
    }
}
