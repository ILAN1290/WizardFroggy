using UnityEngine;

public class RoguelikeExit : MonoBehaviour
{
    private RoguelikeSession session;

    public void Initialize(RoguelikeSession owner)
    {
        session = owner;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || session == null) return;
        bool pressed = InputManager.instance != null
            ? InputManager.instance.InteractionInput
            : UnityEngine.Input.GetKeyDown(KeyCode.E);
        if (!pressed) return;

        if (!session.IsCurrentRoomCleared)
        {
            session.ShowHint("Сначала победите всех призраков!");
            return;
        }

        session.ReturnToLobby();
    }
}
