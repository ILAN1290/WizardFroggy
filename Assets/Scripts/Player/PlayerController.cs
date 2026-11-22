using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.VisualScripting;
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    private Vector2 movement;
    private Vector2 externalVelocity;
    private Vector2 pendingExternalForce;
    private Vector2 windVelocity;
    private Vector2 pendingWindForce;
    private Vector2 lookDirection = new Vector2(0, 1);  
    public Vector2 FacingDirection => lookDirection == Vector2.zero ? Vector2.up : lookDirection.normalized;
    private Rigidbody2D rb;
    private BoxCollider2D movementCollider;
    private PlayerEffects playerEffects;

    /// <summary>
    /// Thin collider at the bottom of the sprite, used for movement and
    /// world-object occlusion. The original Player collider remains for hits.
    /// </summary>
    public Collider2D MovementCollider => movementCollider;


    [Header("Invincibility Settings")]
    [SerializeField] private float speedMultiplier = 2.1f;
    [HideInInspector] public bool isInvincible = false;
    public bool canBeInvincible;

    [Header("Projectile Settings")]
    [SerializeField] private float projectileCooldown = 0.3f;
    private float projectileForce = 1000f;
    public GameObject projectilePrefab;
    public bool canShoot;
    Vector2 projectileDirection = new Vector2(0, 1);
    private float cooldownTimer;


    [Header("Parry")]
    [SerializeField] private Vector3 shieldScale = new Vector3(2f, 2f, 2f); // ������ ����
    public bool canParry;
    [HideInInspector] public Parry parry;

    //Mana
    [Header("Mana")]
    [SerializeField] private PlayerMana mana;
    [SerializeField] private float spendingManaByProjectile = 30f;
    [SerializeField] private float spendingManaByInvincibilityBySecond = 10f;
    [SerializeField] private float spendingManaByParry = 10f;

    [Header("Sound")]
    [SerializeField] private AudioClip ballLightningSound;
    [SerializeField] private AudioClip chargeSound;
    private AudioSource audioSource;


    //Animations
    [HideInInspector] public Animator animator;

    //ABC
    [HideInInspector] public bool hasABC;

    //Health
    private PlayerHealth playerHealth;
    private StaffData equippedStaff;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerEffects = GetComponent<PlayerEffects>();
        parry = GetComponentInChildren<Parry>();
        animator = GetComponent<Animator>();
        cooldownTimer = projectileCooldown;
        audioSource = GetComponent<AudioSource>(); 
        playerHealth = GetComponent<PlayerHealth>();
        InitializeMovementHitbox();
        ApplyEquippedStaff();
    }

    private void InitializeMovementHitbox()
    {
        // The original Player-tagged collider remains enabled for projectiles,
        // attacks and doorway triggers, but no longer blocks physical movement.
        Collider2D hitReactionCollider = GetComponent<Collider2D>();
        if (hitReactionCollider != null)
            hitReactionCollider.isTrigger = true;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            SpriteRenderer[] childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            float largestArea = 0f;
            for (int i = 0; i < childRenderers.Length; i++)
            {
                SpriteRenderer candidate = childRenderers[i];
                if (candidate == null || candidate.sprite == null || !candidate.enabled)
                    continue;

                float area = candidate.bounds.size.x * candidate.bounds.size.y;
                if (area > largestArea)
                {
                    largestArea = area;
                    spriteRenderer = candidate;
                }
            }
        }

        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        Bounds spriteBounds = spriteRenderer.bounds;
        Camera camera = Camera.main;
        float pixelsPerWorldUnitY = camera != null && camera.orthographic
            ? (camera.pixelHeight > 0 ? camera.pixelHeight : Mathf.Max(1, Screen.height)) /
                Mathf.Max(0.01f, camera.orthographicSize * 2f)
            : Mathf.Max(1, Screen.height) / 12f;

        const float movementHitboxHeightPixels = 15f;
        float movementHitboxHeight = movementHitboxHeightPixels /
            Mathf.Max(1f, pixelsPerWorldUnitY);

        Vector3 worldCentre = new Vector3(
            spriteBounds.center.x,
            spriteBounds.min.y + movementHitboxHeight * 0.5f,
            transform.position.z);
        Vector3 localCentre = transform.InverseTransformPoint(worldCentre);
        Vector3 parentScale = transform.lossyScale;

        GameObject movementHitboxObject = new GameObject("Movement Hitbox • 15px");
        movementHitboxObject.layer = gameObject.layer;
        movementHitboxObject.transform.SetParent(transform, false);
        movementHitboxObject.transform.localPosition = localCentre;
        movementHitboxObject.transform.localRotation = Quaternion.identity;

        movementCollider = movementHitboxObject.AddComponent<BoxCollider2D>();
        movementCollider.size = new Vector2(
            spriteBounds.size.x / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
            movementHitboxHeight / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)));
        movementCollider.isTrigger = false;
    }

    private void ApplyEquippedStaff()
    {
        StaffData selected = PlayerRunData.EnsureInstance().EquippedStaff;
        if (selected == null || selected == equippedStaff) return;
        equippedStaff = selected;
        projectileCooldown = equippedStaff.ShotCooldown;
        spendingManaByProjectile = equippedStaff.manaCost;
        cooldownTimer = Mathf.Clamp(cooldownTimer, 0f, projectileCooldown);
    }

    private void Shoot()
    {
        ApplyEquippedStaff();
        if(canShoot && !isInvincible && mana.CurrentMana >= spendingManaByProjectile && InputManager.instance.ShootInput)
        {
            Launch();
        }
    }

    // Adds physics forces without letting the movement controller erase them
    // on the next FixedUpdate (used by wind, knockback and boss abilities).
    public void AddExternalForce(Vector2 force)
    {
        pendingExternalForce += force * Time.deltaTime;
    }

    // Wind is bounded so it pushes strongly without taking permanent control from input.
    public void AddWindForce(Vector2 force)
    {
        pendingWindForce += force * Time.deltaTime;
    }

    // Boss wind is a temporary external force. Clear both accumulated and pending
    // values when the attack ends so the player does not keep drifting afterwards.
    public void ClearWindForce()
    {
        windVelocity = Vector2.zero;
        pendingWindForce = Vector2.zero;
    }

    private void Parry()
    {
        if (InputManager.instance.ParryInput && !isInvincible && canParry && mana.CurrentMana >= spendingManaByParry)
        {
            parry.Activate(shieldScale, spendingManaByParry);
        }
    }
    private void Move()
    {
        movement = InputManager.instance.PlayerMovement;
        if(!Mathf.Approximately(movement.x, 0.0f) || !Mathf.Approximately(movement.y, 0.0f))
        {
            lookDirection = movement; 
        }

        animator.SetFloat("MoveX", lookDirection.x);
        animator.SetFloat("MoveY", lookDirection.y);
        animator.SetFloat("Speed", movement.magnitude);

    }
    private void Invincibility()
    {
        if(canBeInvincible)
        {
            if (InputManager.instance.InvincibilityInput && mana.CurrentMana >= 1f)
            {
                
                audioSource.clip = ballLightningSound;
                audioSource.volume = 0.3f;
                audioSource.loop = true;
                audioSource.Play();
                gameObject.layer = LayerMask.NameToLayer("Invincibility");
                if (movementCollider != null)
                    movementCollider.gameObject.layer = gameObject.layer;
                isInvincible = true;
                mana.isSpending = true;
                animator.SetBool("Is Invincible", true);
            }
            if (InputManager.instance.InvincibilityRelease || InputManager.PlayerInput.currentActionMap.name != "Player" || mana.CurrentMana <= 1f)
            {
                audioSource.clip = null;
                audioSource.loop = false;
                audioSource.volume = 1f;
                audioSource.Stop();
                gameObject.layer = LayerMask.NameToLayer("Player");
                if (movementCollider != null)
                    movementCollider.gameObject.layer = gameObject.layer;
                isInvincible = false;
                mana.isSpending = false;
                animator.SetBool("Is Invincible", false);

            }
        }
        
    }
    void Update()
    {
        Move();
        Shoot();
        Invincibility();
        Parry();


        if (cooldownTimer != projectileCooldown)
        {
            cooldownTimer = Mathf.Clamp(cooldownTimer + Time.deltaTime, 0, projectileCooldown);
        }       
    }


    void FixedUpdate()
    {
        externalVelocity += pendingExternalForce / Mathf.Max(0.01f, rb.mass);
        pendingExternalForce = Vector2.zero;

        // External impulses decay naturally, but remain additive to player input.
        externalVelocity *= Mathf.Exp(-1.5f * Time.fixedDeltaTime);
        externalVelocity = Vector2.ClampMagnitude(externalVelocity, 6f);

        windVelocity += pendingWindForce / Mathf.Max(0.01f, rb.mass);
        pendingWindForce = Vector2.zero;
        windVelocity *= Mathf.Exp(-3.5f * Time.fixedDeltaTime);
        windVelocity = Vector2.ClampMagnitude(windVelocity, Mathf.Max(0f, moveSpeed * 0.75f));

        float potionSpeedMultiplier = playerEffects != null
            ? playerEffects.SpeedMultiplier : 1f;
        Vector2 controlledVelocity = movement.normalized *
            moveSpeed * potionSpeedMultiplier;
        if (isInvincible)
        {
            controlledVelocity *= speedMultiplier;
            mana.SpendMana(spendingManaByInvincibilityBySecond * Time.fixedDeltaTime);
        }

        rb.linearVelocity = controlledVelocity + externalVelocity + windVelocity;
    }
    private void  Launch()
    {
        if(cooldownTimer == projectileCooldown)
        {
            AudioManager.instance.PlaySoundFXClip(chargeSound, transform, 0.1f);
            cooldownTimer = 0;
            mana.SpendMana(spendingManaByProjectile);
            Vector3 spawnPosition = rb.position + Vector2.up * 0.5f;
            Transform projectileParent = RoguelikeSession.ProjectileRoot;
            GameObject projectileObject = projectileParent != null
                ? Instantiate(projectilePrefab, spawnPosition, Quaternion.identity, projectileParent)
                : Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
            Projectile projectile = projectileObject.GetComponent<Projectile>();
            PlayerEffects effects = GetComponent<PlayerEffects>();
            float staffDamage = equippedStaff != null ? equippedStaff.damage : 7f;
            float damageMultiplier = effects != null ? effects.DamageMultiplier : 1f;
            projectile.SetDamage(staffDamage * damageMultiplier);
            projectile.Launch(FacingDirection, projectileForce);
        }
    }
}
