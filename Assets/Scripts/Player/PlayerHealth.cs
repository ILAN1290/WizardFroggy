using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject deathMenu;
    [SerializeField] private AudioSource burningAudioSource;
    [SerializeField] private Animator burningAnimator;
    private string currentCondition = null;
    private PlayerController playerController;
    private float currentHealth;
    private Coroutine burnCoroutine;
    private PlayerEffects effects;
    private bool initialized;

    public float CurrentHealth
    {
        get
        {
            if(currentHealth < 0)
            {
                return 0;
            }
            else
            {
                return currentHealth;
            }
        }
    }

    public float MaxHealth { get { return maxHealth; } }
    void Start()
    {
        playerController = GetComponent<PlayerController>();
        effects = GetComponent<PlayerEffects>();
        currentHealth = PlayerRunData.EnsureInstance().LoadHealth(maxHealth);
        initialized = true;
    }
    private void Update()
    {
        if(currentCondition != null && playerController.canBeInvincible)
        {
            if(InputManager.instance.InvincibilityInput)
            {
                burningAnimator.SetBool(currentCondition, false);
            }
            else if(InputManager.instance.InvincibilityRelease || InputManager.PlayerInput.currentActionMap.name != "Player")
            {
                burningAnimator.SetBool(currentCondition, true);
            }
        }
        
    }

    public void Healing(float health)
    {
        currentHealth = Mathf.Clamp(currentHealth + health, 0, maxHealth);
    }
    public void TakeDamage(float damage)
    {
        if (effects != null && effects.IsInvincible) return;
        currentHealth -= damage;
        Debug.Log($"����� ������� {damage} �����. ������� ��������: {currentHealth}");
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void StartBurning(float burnDamage, float duration, string color)
    {
        if (effects != null && effects.IsInvincible) return;
        if (burnCoroutine != null)
        {
            StopCoroutine(burnCoroutine); // ���� �������� ��� �����, ���������� ������
        }
        else if(burnCoroutine == null)
        {
            burningAudioSource.Play();  
        }

        burnCoroutine = StartCoroutine(BurnEffect(burnDamage, duration, color));
    }

    private IEnumerator BurnEffect(float burnDamage, float duration, string color)
    {
        
        if (color == "Red") //���� ����� �������
        {
            if(currentCondition == "Green")
            {
                burningAnimator.SetBool("Green", false);
            }
            if(!burningAnimator.GetBool("Red"))
            {
                burningAnimator.SetBool("Red", true);
                currentCondition = "Red";
            }
        }
        else if (color == "Green") //���� ����� �������
        {
            if (currentCondition == "Red")
            {
                burningAnimator.SetBool("Red", false);
            }
            if (!burningAnimator.GetBool("Green"))
            {
                burningAnimator.SetBool("Green", true);
                currentCondition = "Green";
            }
        }
        float elapsed = 0f;

        while (elapsed < duration)
        {
            TakeDamage(burnDamage * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }
        burningAnimator.SetBool(currentCondition, false);
        burnCoroutine = null;
        currentCondition = null;
        burningAudioSource.Stop();
    }

    private void Die()
    {
        // In the roguelike, death ends the expedition and returns the player to the lobby.
        if (RoguelikeSession.Instance != null && RoguelikeSession.Instance.HandlePlayerDeath())
            return;

        if (gameManager != null && !gameManager.isGoodEndingActive && !gameManager.isBadEndingActive)
        {
            gameManager.GameOver();
            gameManager.StartBadEnding();
        }
    }

    private void OnDisable()
    {
        if (initialized && PlayerRunData.Instance != null)
            PlayerRunData.Instance.SaveHealth(currentHealth);
    }

    private void OnDestroy()
    {
        if (initialized && PlayerRunData.Instance != null)
            PlayerRunData.Instance.SaveHealth(currentHealth);
    }
}
