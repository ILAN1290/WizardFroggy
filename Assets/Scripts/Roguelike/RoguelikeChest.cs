using UnityEngine;

public class RoguelikeChest : MonoBehaviour
{
    private RoguelikeSession session;
    private bool opened;

    public void Initialize(RoguelikeSession owner)
    {
        session = owner;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (opened || !other.CompareTag("Player") || !InteractionPressed()) return;
        opened = true;
        if (session != null) session.OpenChest(transform.position);
        Destroy(gameObject);
    }

    private bool InteractionPressed()
    {
        return InputManager.instance != null
            ? InputManager.instance.InteractionInput
            : UnityEngine.Input.GetKeyDown(KeyCode.E);
    }
}
